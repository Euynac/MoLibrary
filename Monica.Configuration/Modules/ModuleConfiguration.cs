using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Configuration.Annotations;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Facades;
using Monica.Configuration.Metrics;
using Monica.Configuration.Models;
using Monica.Configuration.Projection;
using Monica.Configuration.Services;
using Monica.Configuration.Services.Support;
using Monica.Configuration.Stores.File;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the Monica.Configuration module.
/// </summary>
public static class ModuleConfigurationBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers the schema-first Monica configuration module.
        /// </summary>
        /// <param name="action">Optional module option configuration.</param>
        /// <returns>The module guide.</returns>
        public static ModuleConfigurationGuide AddConfiguration(Action<ModuleConfigurationOption>? action = null)
        {
            return new ModuleConfigurationGuide().Register(action);
        }
    }
}

/// <summary>
/// Monica configuration module.
/// </summary>
[ModuleKey(BuiltInModuleKey.Configuration)]
public sealed class ModuleConfiguration
    : ModuleBase<ModuleConfiguration, ModuleConfigurationOption, ModuleConfigurationGuide>, IBusinessTypeIterator
{
    private static readonly MethodInfo ADD_OPTIONS_METHOD = GetRequiredGenericMethod(
        typeof(OptionsServiceCollectionExtensions),
        nameof(OptionsServiceCollectionExtensions.AddOptions),
        [typeof(IServiceCollection)]);

    private static readonly MethodInfo BIND_METHOD = GetRequiredGenericMethod(
        typeof(OptionsBuilderConfigurationExtensions),
        nameof(OptionsBuilderConfigurationExtensions.Bind),
        [typeof(OptionsBuilder<>), typeof(IConfiguration)]);

    private static readonly MethodInfo VALIDATE_DATA_ANNOTATIONS_METHOD = GetRequiredGenericMethod(
        typeof(OptionsBuilderDataAnnotationsExtensions),
        nameof(OptionsBuilderDataAnnotationsExtensions.ValidateDataAnnotations),
        [typeof(OptionsBuilder<>)]);

    private readonly ConfigurationDefinitionRegistry _definitionRegistry = new();
    private readonly ConfigurationSchemaHasher _schemaHasher;
    private readonly ConfigurationDefinitionScanner _definitionScanner;
    private readonly MonicaConfigurationProviderAccessor _providerAccessor = new();
    private IConfiguration? _configuration;
    private IServiceCollection? _services;

    /// <summary>
    /// Creates a Monica configuration module instance.
    /// </summary>
    /// <param name="option">The module options.</param>
    public ModuleConfiguration(ModuleConfigurationOption option)
        : this(option, new ConfigurationSchemaHasher())
    {
    }

    private ModuleConfiguration(ModuleConfigurationOption option, ConfigurationSchemaHasher schemaHasher)
        : base(option)
    {
        _schemaHasher = schemaHasher;
        _definitionScanner = new ConfigurationDefinitionScanner(_schemaHasher);
    }

    /// <inheritdoc />
    public override void ConfigureBuilder(IHostApplicationBuilder builder)
    {
        _configuration = builder.Configuration;
        // This appends Monica's effective-value projection after the host's bootstrap providers.
        // If callers add more Microsoft configuration providers later, their ordering relative to Monica
        // should become an explicit module option or guide method instead of relying on call order.
        builder.Configuration.Add(new MonicaConfigurationSource(_providerAccessor));
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        _services = services;
        services.TryAddSingleton<IConfigurationDefinitionRegistry>(_definitionRegistry);
        services.TryAddSingleton<IConfigurationStoreStateTracker, ConfigurationStoreStateTracker>();
        services.TryAddSingleton<IConfigurationHistoryService, ConfigurationHistoryService>();
        services.TryAddSingleton<IConfigurationMutationService, ConfigurationMutationService>();
        services.TryAddSingleton<IConfigurationMutationGroupService, ConfigurationMutationGroupService>();
        services.TryAddSingleton<IConfigurationRollbackService, ConfigurationRollbackService>();
        services.TryAddSingleton<IConfigurationReloadCoordinator, ConfigurationProviderReloadCoordinator>();
        services.TryAddSingleton(_schemaHasher);
        services.TryAddSingleton<ConfigurationStoredValueCodec>();
        services.TryAddSingleton<ConfigurationValidationCoordinator>();
        services.TryAddSingleton<ConfigurationPathProjector>();
        services.TryAddSingleton<ConfigurationEffectiveValuePatchEngine>();
        services.TryAddSingleton<ConfigurationEffectiveValueDocumentEditor>();
        services.TryAddSingleton<ConfigurationEffectiveValueSeedFactory>();
        services.TryAddSingleton(_providerAccessor);
        services.TryAddSingleton<ConfigurationMetricsRecorder>();
        services.AddHostedService<MonicaConfigurationProviderActivationHostedService>();
        services.TryAddSingleton<ConfigurationFacade>();
    }

    /// <inheritdoc />
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type is { IsClass: true, IsAbstract: false }
                && type.GetCustomAttribute<ConfigurationAttribute>(inherit: false) is { } attribute)
            {
                RegisterConfigurationType(type, attribute);
            }

            yield return type;
        }
    }

    private void RegisterConfigurationType(Type optionsType, ConfigurationAttribute attribute)
    {
        var definition = _definitionScanner.Scan(optionsType);
        _definitionRegistry.Register(definition);
        RegisterOptionsBinding(optionsType, definition.SectionPath);
    }

    private void RegisterOptionsBinding(Type optionsType, string sectionPath)
    {
        if (_services is null)
        {
            throw new InvalidOperationException($"{nameof(ModuleConfiguration)} services have not been configured.");
        }

        if (_configuration is null)
        {
            throw new InvalidOperationException(
                $"{nameof(ModuleConfiguration)} requires an {nameof(IHostApplicationBuilder)} configuration instance. Call builder.UseMonica() after registering modules.");
        }

        var optionsBuilder = ADD_OPTIONS_METHOD.MakeGenericMethod(optionsType).Invoke(null, [_services])
            ?? throw new InvalidOperationException($"Failed to create OptionsBuilder for '{optionsType.FullName}'.");

        var configurationSection = _configuration.GetSection(sectionPath);
        BIND_METHOD.MakeGenericMethod(optionsType).Invoke(null, [optionsBuilder, configurationSection]);
        VALIDATE_DATA_ANNOTATIONS_METHOD.MakeGenericMethod(optionsType).Invoke(null, [optionsBuilder]);
    }

    private static MethodInfo GetRequiredGenericMethod(Type extensionType, string methodName, IReadOnlyList<Type> parameterTypeDefinitions)
    {
        return extensionType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(method => MatchesGenericMethod(method, methodName, parameterTypeDefinitions))
            ?? throw new InvalidOperationException($"Required method '{extensionType.FullName}.{methodName}' was not found.");
    }

    private static bool MatchesGenericMethod(MethodInfo method, string methodName, IReadOnlyList<Type> parameterTypeDefinitions)
    {
        if (!method.IsGenericMethodDefinition || method.Name != methodName)
        {
            return false;
        }

        var parameters = method.GetParameters();
        if (parameters.Length != parameterTypeDefinitions.Count)
        {
            return false;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            var actual = parameters[i].ParameterType;
            var expected = parameterTypeDefinitions[i];
            if (actual.IsGenericType)
            {
                actual = actual.GetGenericTypeDefinition();
            }

            if (actual != expected)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Fluent guide for Monica.Configuration.
/// </summary>
public sealed class ModuleConfigurationGuide
    : ModuleGuide<ModuleConfiguration, ModuleConfigurationOption, ModuleConfigurationGuide>
{
    /// <summary>
    /// Uses the file-backed store bundle for effective values, history, and metadata.
    /// </summary>
    /// <param name="configure">Optional file store configuration.</param>
    /// <returns>The module guide.</returns>
    public ModuleConfigurationGuide UseFileConfigurationStore(Action<ConfigurationFileStoreOptions>? configure = null)
    {
        ConfigureServices(context =>
        {
            context.Services.AddOptions<ConfigurationFileStoreOptions>();
            if (configure is not null)
            {
                context.Services.Configure(configure);
            }

            context.Services.TryAddSingleton<FileConfigurationStore>();
            context.Services.TryAddSingleton<IConfigurationEffectiveValueStore>(provider => provider.GetRequiredService<FileConfigurationStore>());
            context.Services.TryAddSingleton<IConfigurationHistoryStore>(provider => provider.GetRequiredService<FileConfigurationStore>());
            context.Services.TryAddSingleton<IConfigurationMetadataStore>(provider => provider.GetRequiredService<FileConfigurationStore>());
        });
        return this;
    }
}

/// <summary>
/// Module options for Monica.Configuration.
/// </summary>
public sealed class ModuleConfigurationOption : ModuleOptions<ModuleConfiguration>;
