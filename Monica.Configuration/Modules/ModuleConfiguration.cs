using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Configuration.Annotations;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Binding;
using Monica.Configuration.Bootstrap;
using Monica.Configuration.Exceptions;
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
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the schema-first Monica configuration module.
        /// </summary>
        /// <param name="action">Optional module option configuration.</param>
        /// <returns>The module guide.</returns>
        public ModuleConfigurationGuide AddConfiguration(Action<ModuleConfigurationOption>? action = null)
        {
            return builder.AddModule<ModuleConfiguration, ModuleConfigurationOption, ModuleConfigurationGuide>(action);
        }
    }
}

/// <summary>
/// Monica configuration module.
/// </summary>
[ModuleKey(BuiltInModuleKey.Configuration)]
public sealed class ModuleConfiguration
    : WebModuleBase<ModuleConfiguration, ModuleConfigurationOption, ModuleConfigurationGuide>, IBusinessTypeIterator
{
    private static readonly MethodInfo ADD_OPTIONS_METHOD = GetRequiredGenericMethod(
        typeof(OptionsServiceCollectionExtensions),
        nameof(OptionsServiceCollectionExtensions.AddOptions),
        [typeof(IServiceCollection)]);

    private static readonly MethodInfo BIND_OPTIONS_METHOD = GetRequiredGenericMethod(
        typeof(MonicaConfigurationBinder),
        nameof(MonicaConfigurationBinder.BindOptions),
        [typeof(OptionsBuilder<>), typeof(IConfiguration)]);

    private readonly ConfigurationDefinitionRegistry _definitionRegistry = new();
    private readonly ConfigurationRuntimeContext _runtimeContext = new();
    private readonly ConfigurationSchemaHasher _schemaHasher;
    private readonly ConfigurationDefinitionScanner _definitionScanner;
    private readonly MonicaConfigurationProviderAccessor _providerAccessor = new();
    private ConfigurationDefinitionAnalysis _definitionAnalysis = ConfigurationDefinitionAnalysis.Empty;
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
        _definitionScanner = new ConfigurationDefinitionScanner(_schemaHasher, Option.DefaultSectionPathConvention);
    }

    /// <inheritdoc />
    public override bool CanDowngradeToNonWebModule()
    {
        return true;
    }

    /// <inheritdoc />
    public override void ConfigureBuilder(IHostApplicationBuilder builder)
    {
        _runtimeContext.Capture(builder.Configuration);
        // This appends Monica's effective-value projection after the host's bootstrap providers.
        // If callers add more Microsoft configuration providers later, their ordering relative to Monica
        // should become an explicit module option or guide method instead of relying on call order.
        builder.Configuration.Add(new MonicaConfigurationSource(_providerAccessor));
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        if (!Enum.IsDefined(Option.RuntimeValidationBehavior))
        {
            throw new InvalidOperationException(
                $"Unsupported {nameof(ConfigurationRuntimeValidationBehavior)} value '{Option.RuntimeValidationBehavior}'.");
        }

        _services = services;
        services.TryAddSingleton<IConfigurationDefinitionRegistry>(_definitionRegistry);
        services.TryAddSingleton<ConfigurationDefinitionResolver>();
        services.TryAddSingleton<ConfigurationEffectiveStateReader>();
        services.TryAddSingleton<IConfigurationStoreStateTracker, ConfigurationStoreStateTracker>();
        services.TryAddSingleton<IConfigurationHistoryService, ConfigurationHistoryService>();
        services.TryAddSingleton<IConfigurationMutationBatchStore, SequentialConfigurationMutationBatchStore>();
        services.TryAddSingleton<IConfigurationMutationGroupService, ConfigurationMutationGroupService>();
        services.TryAddSingleton<ConfigurationRuntimeSnapshotLock>();
        services.TryAddSingleton<IConfigurationMutationGroupApplyService, ConfigurationMutationGroupApplyService>();
        services.TryAddSingleton<IConfigurationRollbackService, ConfigurationRollbackService>();
        services.TryAddSingleton<IConfigurationUnifiedVersionService, ConfigurationUnifiedVersionService>();
        services.TryAddSingleton<IConfigurationUnifiedVersionCoordinator, ConfigurationUnifiedVersionCoordinator>();
        services.TryAddSingleton<ConfigurationEffectiveSnapshotReader>();
        services.TryAddSingleton<ConfigurationUnifiedVersionSnapshotFactory>();
        services.TryAddSingleton<ConfigurationUnifiedVersionRollbackPreviewFactory>();
        services.TryAddSingleton<ConfigurationRollbackPersistencePlanner>();
        services.TryAddSingleton<IConfigurationReloadCoordinator, ConfigurationProviderReloadCoordinator>();
        services.TryAddSingleton<IConfigurationReloadSignalReceiver, ConfigurationReloadSignalReceiver>();
        services.TryAddSingleton<ConfigurationReloadNotificationDispatcher>();
        services.TryAddSingleton<IConfigurationReloadBroadcastService, ConfigurationReloadBroadcastService>();
        services.TryAddSingleton(_schemaHasher);
        services.TryAddSingleton<ConfigurationStoredValueCodec>();
        services.TryAddSingleton<ConfigurationValueValidationEngine>();
        services.TryAddSingleton<ConfigurationValidationCoordinator>();
        services.TryAddSingleton<IConfigurationCandidateValidationService, ConfigurationCandidateValidationService>();
        services.TryAddSingleton<ConfigurationMutationPlanner>();
        services.TryAddSingleton<ConfigurationPathProjector>();
        services.TryAddSingleton<ConfigurationEffectiveValuePatchEngine>();
        services.TryAddSingleton<ConfigurationEffectiveValueDocumentEditor>();
        services.TryAddSingleton<ConfigurationEffectiveValueSeedFactory>();
        services.TryAddSingleton<IConfigurationSourceInspector, ConfigurationSourceInspector>();
        services.TryAddSingleton<IConfigurationRuntimeValidationService, ConfigurationRuntimeValidationService>();
        services.TryAddSingleton<IConfigurationRuntimeReloadService, ConfigurationRuntimeReloadService>();
        services.TryAddSingleton<IConfigurationJsonFileSourceWriter, ConfigurationJsonFileSourceWriter>();
        services.TryAddSingleton(_runtimeContext);
        services.TryAddSingleton(_providerAccessor);
        services.TryAddSingleton<ConfigurationMetricsRecorder>();
        services.TryAddSingleton<ConfigurationPublisherIdentityProvider>();
        services.TryAddSingleton<MonicaConfigurationProviderActivationCoordinator>();
        services.AddHostedService<MonicaConfigurationProviderActivationHostedService>();
        services.TryAddSingleton<ConfigurationFacade>();
    }

    /// <inheritdoc />
    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        var activationCoordinator = app.ApplicationServices.GetRequiredService<MonicaConfigurationProviderActivationCoordinator>();
        activationCoordinator.ActivateAsync(CancellationToken.None).GetAwaiter().GetResult();

        var validationService = app.ApplicationServices.GetRequiredService<IConfigurationRuntimeValidationService>();
        var report = validationService.GetReport();
        if (report.IsValid)
        {
            return;
        }

        if (Option.RuntimeValidationBehavior == ConfigurationRuntimeValidationBehavior.FailFast)
        {
            throw new ConfigurationRuntimeValidationException(report);
        }

        Logger.LogWarning(
            "{ConfigurationRuntimeValidationDiagnostic}",
            ConfigurationRuntimeValidationMessageFormatter.FormatDiagnosticReport(report));
    }

    /// <inheritdoc />
    protected override int GetConfigureApplicationBuilderOrder()
    {
        return -1000;
    }

    /// <inheritdoc />
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        var configurationTypes = new List<Type>();
        foreach (var type in types)
        {
            if (type is { IsClass: true, IsAbstract: false }
                && type.GetCustomAttribute<ConfigurationAttribute>(inherit: false) is not null)
            {
                configurationTypes.Add(type);
            }

            yield return type;
        }

        if (configurationTypes.Count == 0)
        {
            yield break;
        }

        var typesToAnalyze = configurationTypes.ToArray();
        ScheduleCompositionWork(
            "build-configuration-definitions",
            () => BuildDefinitions(typesToAnalyze),
            CommitDefinitions,
            ModuleCompositionWorkDeadline.BeforePostConfigureServices);
    }

    private void BuildDefinitions(IReadOnlyList<Type> optionsTypes)
    {
        var analysis = ConfigurationDefinitionAnalysis.Create(_definitionScanner, optionsTypes);
        var conflicts = ValidateDefinitions(analysis.Registrations);
        Volatile.Write(ref _definitionAnalysis, analysis.WithSectionPathConflicts(conflicts));
    }

    private void CommitDefinitions()
    {
        var services = _services
            ?? throw new InvalidOperationException($"{nameof(ModuleConfiguration)} services have not been configured.");
        var analysis = Volatile.Read(ref _definitionAnalysis);

        foreach (var registration in analysis.Registrations)
        {
            RegisterOptionsBinding(
                services,
                registration.OptionsType,
                registration.Definition.SectionPath,
                registration.Definition.DefinitionKey);
        }

        _definitionRegistry.RegisterRange(
            analysis.Registrations.Select(static registration => registration.Definition));

        foreach (var conflict in analysis.SectionPathConflicts)
        {
            Logger.LogWarning(
                "Duplicate Monica configuration section path '{SectionPath}' is used by definitions '{ExistingDefinitionKey}' and '{NewDefinitionKey}'.",
                conflict.Existing.SectionPath,
                conflict.Existing.DefinitionKey,
                conflict.Duplicate.DefinitionKey);
        }
    }

    private IReadOnlyList<ConfigurationSectionPathConflict> ValidateDefinitions(
        IReadOnlyList<ConfigurationDefinitionRegistration> registrations)
    {
        var duplicateKey = registrations
            .GroupBy(static registration => registration.Definition.DefinitionKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Skip(1).Any());
        if (duplicateKey is not null)
        {
            var types = string.Join(
                ", ",
                duplicateKey.Select(static registration => registration.OptionsType.FullName)
                    .Order(StringComparer.Ordinal));
            throw new InvalidOperationException(
                $"Configuration definition key '{duplicateKey.Key}' is declared by multiple options types: {types}.");
        }

        var definitions = _definitionRegistry.GetAll()
            .Concat(registrations.Select(static registration => registration.Definition))
            .OrderBy(static definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var conflicts = new List<ConfigurationSectionPathConflict>();
        foreach (var sectionGroup in definitions.GroupBy(
                     static definition => definition.SectionPath,
                     StringComparer.OrdinalIgnoreCase))
        {
            var distinctDefinitions = sectionGroup
                .GroupBy(static definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .OrderBy(static definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (distinctDefinitions.Length < 2)
            {
                continue;
            }

            foreach (var duplicate in distinctDefinitions.Skip(1))
            {
                var conflict = new ConfigurationSectionPathConflict(distinctDefinitions[0], duplicate);
                if (Option.DuplicateSectionPathBehavior == ConfigurationDuplicateSectionPathBehavior.FailFast)
                {
                    throw new InvalidOperationException(BuildDuplicateSectionPathMessage(conflict));
                }

                conflicts.Add(conflict);
            }
        }

        return conflicts;
    }

    private static string BuildDuplicateSectionPathMessage(ConfigurationSectionPathConflict conflict)
    {
        return
            $"Configuration section path '{conflict.Existing.SectionPath}' is used by both '{conflict.Existing.DefinitionKey}' and '{conflict.Duplicate.DefinitionKey}'. " +
            $"Set an explicit {nameof(ConfigurationAttribute.SectionPath)}, change {nameof(ModuleConfigurationOption.DefaultSectionPathConvention)}, " +
            $"or set {nameof(ModuleConfigurationOption.DuplicateSectionPathBehavior)} to {nameof(ConfigurationDuplicateSectionPathBehavior.Warning)}.";
    }

    private void RegisterOptionsBinding(
        IServiceCollection services,
        Type optionsType,
        string sectionPath,
        string definitionKey)
    {
        var optionsBuilder = ADD_OPTIONS_METHOD.MakeGenericMethod(optionsType).Invoke(null, [services])
            ?? throw new InvalidOperationException($"Failed to create OptionsBuilder for '{optionsType.FullName}'.");

        var configurationSection = _runtimeContext.Configuration.GetSection(sectionPath);
        BIND_OPTIONS_METHOD.MakeGenericMethod(optionsType).Invoke(null, [optionsBuilder, configurationSection]);
        if (Option.RuntimeValidationBehavior == ConfigurationRuntimeValidationBehavior.FailFast)
        {
            RegisterOptionsValidator(services, optionsType, definitionKey);
        }
    }

    private static void RegisterOptionsValidator(
        IServiceCollection services,
        Type optionsType,
        string definitionKey)
    {
        var serviceType = typeof(IValidateOptions<>).MakeGenericType(optionsType);
        var validatorType = typeof(MonicaConfigurationOptionsValidator<>).MakeGenericType(optionsType);
        services.AddSingleton(serviceType, provider => ActivatorUtilities.CreateInstance(provider, validatorType, definitionKey));
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
    : WebModuleGuide<ModuleConfiguration, ModuleConfigurationOption, ModuleConfigurationGuide>
{
    private const int MANAGED_JSON_FILE_BUILDER_ORDER = -2;
    private readonly MonicaEffectiveOptionsReaderConfiguration _effectiveOptionsReaderConfiguration = new();

    /// <summary>
    /// Creates a startup options reader that uses this guide's store and managed JSON source configuration.
    /// </summary>
    /// <param name="builder">The host builder whose environment and configuration roots define startup context.</param>
    /// <param name="bootstrapConfiguration">
    /// Optional bootstrap configuration used as the lowest-priority input. When omitted, <paramref name="builder"/> configuration is used.
    /// </param>
    /// <param name="configure">Optional reader configuration.</param>
    /// <returns>The effective options reader. The caller owns and must dispose the reader.</returns>
    /// <remarks>
    /// The reader is intended for module-registration code that runs before the application service provider exists.
    /// It binds values using the same priority shape as the runtime provider chain: bootstrap configuration, Monica
    /// effective values, and then JSON files registered through <see cref="AddManagedJsonFile"/>.
    /// </remarks>
    public IMonicaEffectiveOptionsReader CreateEffectiveOptionsReader(
        IHostApplicationBuilder builder,
        IConfiguration? bootstrapConfiguration = null,
        Action<MonicaEffectiveOptionsReaderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return _effectiveOptionsReaderConfiguration.CreateReader(
            builder,
            bootstrapConfiguration ?? builder.Configuration,
            configure,
            Logger);
    }

    /// <summary>
    /// Uses an effective-value store factory for readers created before the application service provider exists.
    /// </summary>
    /// <param name="factory">The store factory. The reader owns the returned store instance and disposes it when possible.</param>
    /// <returns>The module guide.</returns>
    /// <remarks>
    /// Built-in store guide methods call this automatically. Custom store providers should call it when they need
    /// <see cref="CreateEffectiveOptionsReader"/> to work before dependency injection is built.
    /// </remarks>
    public ModuleConfigurationGuide UseStartupEffectiveValueStore(Func<IConfigurationEffectiveValueStore> factory)
    {
        _effectiveOptionsReaderConfiguration.UseEffectiveValueStore(factory);
        return this;
    }

    /// <summary>
    /// Enables unified configuration version control.
    /// </summary>
    /// <returns>The module guide.</returns>
    /// <remarks>
    /// Unified version control is disabled by default. After enabling it, register at least one inclusion
    /// filter through <see cref="IncludeUnifiedVersionCategories"/>, <see cref="IncludeUnifiedVersionDefinitions(string[])"/>,
    /// <see cref="IncludeUnifiedVersionDefinitions(Func{ConfigurationDefinition, bool})"/>, or
    /// <see cref="UseUnifiedVersionFilter{TFilter}"/>. The module fails fast if enabled without filters.
    /// </remarks>
    public ModuleConfigurationGuide UseUnifiedVersionControl()
    {
        ConfigureModuleOption(options =>
        {
            options.UnifiedVersionControl.Enabled = true;
        });
        return this;
    }

    /// <summary>
    /// Includes configuration definitions with one of the specified categories in unified versions.
    /// </summary>
    /// <param name="categories">The categories to include.</param>
    /// <returns>The module guide.</returns>
    public ModuleConfigurationGuide IncludeUnifiedVersionCategories(params string[] categories)
    {
        var normalizedCategories = NormalizeFilterValues(categories, nameof(categories));
        UseUnifiedVersionControl();
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IConfigurationUnifiedVersionFilter>(
                new ConfigurationUnifiedVersionCategoryFilter(normalizedCategories));
        }, secondKey: string.Join('|', normalizedCategories));
        return this;
    }

    /// <summary>
    /// Includes configuration definitions with one of the specified definition keys in unified versions.
    /// </summary>
    /// <param name="definitionKeys">The definition keys to include.</param>
    /// <returns>The module guide.</returns>
    public ModuleConfigurationGuide IncludeUnifiedVersionDefinitions(params string[] definitionKeys)
    {
        var normalizedDefinitionKeys = NormalizeFilterValues(definitionKeys, nameof(definitionKeys));
        UseUnifiedVersionControl();
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IConfigurationUnifiedVersionFilter>(
                new ConfigurationUnifiedVersionDefinitionKeyFilter(normalizedDefinitionKeys));
        }, secondKey: string.Join('|', normalizedDefinitionKeys));
        return this;
    }

    /// <summary>
    /// Includes configuration definitions accepted by a predicate in unified versions.
    /// </summary>
    /// <param name="predicate">The inclusion predicate.</param>
    /// <returns>The module guide.</returns>
    public ModuleConfigurationGuide IncludeUnifiedVersionDefinitions(Func<ConfigurationDefinition, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        UseUnifiedVersionControl();
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IConfigurationUnifiedVersionFilter>(
                new ConfigurationUnifiedVersionPredicateFilter(predicate));
        }, secondKey: Guid.NewGuid().ToString("N"));
        return this;
    }

    /// <summary>
    /// Registers a custom unified version inclusion filter.
    /// </summary>
    /// <typeparam name="TFilter">The filter implementation type.</typeparam>
    /// <returns>The module guide.</returns>
    public ModuleConfigurationGuide UseUnifiedVersionFilter<TFilter>()
        where TFilter : class, IConfigurationUnifiedVersionFilter
    {
        UseUnifiedVersionControl();
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IConfigurationUnifiedVersionFilter, TFilter>();
        }, secondKey: typeof(TFilter).FullName);
        return this;
    }

    /// <summary>
    /// Adds a JSON configuration file after Monica's effective-value provider and records source metadata for the UI.
    /// </summary>
    /// <param name="path">The JSON file path passed to <see cref="JsonConfigurationExtensions.AddJsonFile(IConfigurationBuilder,string,bool,bool)"/>.</param>
    /// <param name="optional">Whether the file is optional.</param>
    /// <param name="reloadOnChange">Whether Microsoft configuration reloads when the file changes.</param>
    /// <param name="configure">Optional Monica source metadata configuration.</param>
    /// <returns>The module guide.</returns>
    public ModuleConfigurationGuide AddManagedJsonFile(
        string path,
        bool optional = true,
        bool reloadOnChange = true,
        Action<ManagedJsonConfigurationSourceOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var options = new ManagedJsonConfigurationSourceOptions
        {
            DisplayName = Path.GetFileName(path)
        };
        configure?.Invoke(options);
        var registration = new ManagedJsonConfigurationSourceRegistration
        {
            Path = path,
            Optional = optional,
            ReloadOnChange = reloadOnChange,
            DisplayName = string.IsNullOrWhiteSpace(options.DisplayName) ? Path.GetFileName(path) : options.DisplayName,
            Description = options.Description,
            IsWritable = options.IsWritable
        };
        _effectiveOptionsReaderConfiguration.AddManagedJsonSource(registration);

        ConfigureBuilder(context =>
        {
            context.HostApplicationBuilder.Configuration.AddJsonFile(
                registration.Path,
                registration.Optional,
                registration.ReloadOnChange);
            ManagedJsonConfigurationSourceRegistry.Add(
                context.HostApplicationBuilder.Configuration,
                registration);
        // The module registry reverses sorted requests during de-duplication; using an order below
        // the module-owned -1 builder request appends this provider after Monica's effective store.
        }, MANAGED_JSON_FILE_BUILDER_ORDER, secondKey: Guid.NewGuid().ToString("N"));

        return this;
    }

    /// <summary>
    /// Uses the file-backed store bundle for effective values, history, and metadata.
    /// </summary>
    /// <param name="configure">Optional file store configuration.</param>
    /// <returns>The module guide.</returns>
    public ModuleConfigurationGuide UseFileConfigurationStore(Action<ConfigurationFileStoreOptions>? configure = null)
    {
        var startupOptions = new ConfigurationFileStoreOptions();
        configure?.Invoke(startupOptions);
        UseStartupEffectiveValueStore(() => new FileConfigurationStore(Options.Create(startupOptions)));

        ConfigureServices(context =>
        {
            context.Services.AddOptions<ConfigurationFileStoreOptions>();
            context.Services.Configure<ConfigurationFileStoreOptions>(options =>
            {
                options.RootDirectory = startupOptions.RootDirectory;
            });

            context.Services.TryAddSingleton<FileConfigurationStore>();
            context.Services.TryAddSingleton<IConfigurationEffectiveValueStore>(provider => provider.GetRequiredService<FileConfigurationStore>());
            context.Services.TryAddSingleton<IConfigurationHistoryStore>(provider => provider.GetRequiredService<FileConfigurationStore>());
            context.Services.TryAddSingleton<IConfigurationMetadataStore>(provider => provider.GetRequiredService<FileConfigurationStore>());
            context.Services.TryAddSingleton<IConfigurationUnifiedVersionStore>(provider => provider.GetRequiredService<FileConfigurationStore>());
        });
        return this;
    }

    private static IReadOnlySet<string> NormalizeFilterValues(IReadOnlyList<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);

        var normalized = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("At least one non-empty value is required.", parameterName);
        }

        return normalized.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Module options for Monica.Configuration.
/// </summary>
public sealed class ModuleConfigurationOption : ModuleOptions<ModuleConfiguration>
{
    /// <summary>
    /// Gets or sets how Monica derives section paths for configuration types that do not set
    /// <see cref="ConfigurationAttribute.SectionPath"/> explicitly.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="ConfigurationSectionPathConvention.ShortTypeName"/>, which binds an options type such as
    /// <c>K8SOptions</c> to the root section <c>K8SOptions</c>. Use
    /// <see cref="ConfigurationSectionPathConvention.ClrFullName"/> when a host intentionally wants namespace-qualified
    /// roots such as <c>Company:Product:K8SOptions</c>.
    /// </remarks>
    public ConfigurationSectionPathConvention DefaultSectionPathConvention { get; set; } =
        ConfigurationSectionPathConvention.ShortTypeName;

    /// <summary>
    /// Gets or sets how Monica handles duplicate resolved section paths across managed configuration definitions.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="ConfigurationDuplicateSectionPathBehavior.FailFast"/> because two definitions bound to the
    /// same section make source inspection, mutation, and bootstrap behavior ambiguous. Use
    /// <see cref="ConfigurationDuplicateSectionPathBehavior.Warning"/> only when a host intentionally accepts the overlap.
    /// </remarks>
    public ConfigurationDuplicateSectionPathBehavior DuplicateSectionPathBehavior { get; set; } =
        ConfigurationDuplicateSectionPathBehavior.FailFast;

    /// <summary>
    /// Gets or sets whether invalid effective Monica-managed values are reported as diagnostics or enforced as
    /// application failures.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="ConfigurationRuntimeValidationBehavior.DiagnosticOnly"/>. Monica still activates the
    /// configured provider, generates the source-aware validation report, logs one warning, and exposes the findings
    /// through its facade and UI, but it does not prevent application startup or managed options resolution. Invalid
    /// values are not made safe by this setting; consumers must tolerate them until an operator corrects the source.
    /// Set this to <see cref="ConfigurationRuntimeValidationBehavior.FailFast"/> when the host must reject startup and
    /// subsequent Microsoft options resolution whenever a managed definition is invalid. Store access, provider
    /// activation, report generation, binding conversion, and definition registration failures remain fatal in both
    /// modes.
    /// </remarks>
    public ConfigurationRuntimeValidationBehavior RuntimeValidationBehavior { get; set; } =
        ConfigurationRuntimeValidationBehavior.DiagnosticOnly;

    /// <summary>
    /// Gets unified configuration version control options.
    /// </summary>
    /// <remarks>
    /// Unified versioning is disabled by default. Use guide methods to enable it and register explicit
    /// inclusion filters for the definitions that should be captured and restorable as unified versions.
    /// </remarks>
    public ConfigurationUnifiedVersionControlOptions UnifiedVersionControl { get; } = new();

    /// <summary>
    /// Gets or sets whether source inventory includes runtime configuration keys that do not belong to
    /// Monica-managed configuration definitions.
    /// </summary>
    /// <remarks>
    /// This is enabled by default so operators can inspect bootstrap, host, and custom provider values from
    /// the configuration storage page. Disable it when a host must hide unmanaged runtime configuration from
    /// Monica.Configuration UI.
    /// </remarks>
    public bool IncludeUnmanagedSourceInventoryItems { get; set; } = true;

    /// <summary>
    /// Gets or sets the stable logical service key used to reconcile configuration metadata published by replicas.
    /// </summary>
    /// <remarks>
    /// Replicas of the same service must use the same value. When omitted, Monica uses the host application name,
    /// which normally matches the entry assembly name. Configure this explicitly when multiple logical services
    /// share an application name or when deployment naming must remain stable across entry-assembly changes.
    /// </remarks>
    public string? PublisherKey { get; set; }

    /// <summary>
    /// Gets or sets the stable identity used to ignore reload notifications produced by this process.
    /// </summary>
    /// <remarks>
    /// The default is generated once when the module option instance is created. Set this explicitly when
    /// host infrastructure already provides a better per-process instance id.
    /// </remarks>
    public string InstanceId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets how long remote Monica projection reload requests are batched before they are applied.
    /// </summary>
    /// <remarks>
    /// The default is 500 milliseconds so multiple mutations in the same burst coalesce into one reload.
    /// </remarks>
    public TimeSpan RemoteReloadDebounceDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets or sets the maximum additional random delay before a remote Monica projection reload is applied.
    /// </summary>
    /// <remarks>
    /// The default is two seconds to reduce thundering-herd pressure when many service instances receive
    /// the same distributed notification.
    /// </remarks>
    public TimeSpan RemoteReloadMaxJitterDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets how long received notification ids are retained for duplicate suppression.
    /// </summary>
    /// <remarks>
    /// The default is five minutes, covering common at-least-once delivery retry windows without retaining
    /// unbounded notification state.
    /// </remarks>
    public TimeSpan RemoteReloadDedupeWindow { get; set; } = TimeSpan.FromMinutes(5);
}
