using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Configuration;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Annotations;
using Monica.Configuration.Extensions;
using Monica.Configuration.Facades;
using Monica.Configuration.Models;
using Monica.Configuration.Models.Internal;
using Monica.Configuration.Providers.History;
using Monica.Configuration.Providers.JsonFile;
using Monica.Configuration.Providers.ProjectCatalog;
using Monica.Configuration.Services;
using Monica.Configuration.Services.Support;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleConfigurationBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the Configuration module.
        /// </summary>
        public static ModuleConfigurationGuide AddConfiguration(Action<ModuleConfigurationOption>? action = null)
        {
            return new ModuleConfigurationGuide().Register(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.Configuration)]
public class ModuleConfiguration(ModuleConfigurationOption option) : WebModuleBase<ModuleConfiguration, ModuleConfigurationOption, ModuleConfigurationGuide>(option), IBusinessTypeIterator
{
    private IServiceCollection _services = null!;
    private MethodInfo _method = null!;

    public override void ConfigureBuilder(IHostApplicationBuilder builder)
    {
        Option.AppConfiguration ??= builder.Configuration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        _services = services;
        ConfigurationRuntime.Setting = Option;
        ConfigurationRuntime.AppConfiguration = GetAppConfiguration();

        services.AddOptions();
        services.AddSingleton<IConfigurationCatalog, ConfigurationCatalogService>();
        services.TryAddSingleton<IConfigurationProjectCatalog, LocalConfigurationProjectCatalog>();
        services.TryAddTransient<IConfigurationHistoryStore, MemoryConfigurationHistoryStore>();
        services.TryAddSingleton<IConfigurationValueWriter, JsonFileConfigurationWriter>();
        services.TryAddSingleton<LocalConfigurationManagementApi>();
        services.TryAddSingleton<IConfigurationManagementApi>(provider =>
            provider.GetRequiredService<LocalConfigurationManagementApi>());
        services.TryAddSingleton<IConfigurationDashboardContext, LocalConfigurationDashboardContext>();
        services.AddScoped<ConfigurationFacade>();

        // if (Option is { UseDaprProvider: true, AppConfiguration: ConfigurationManager manager})
        // {
        //     Logger.LogDebug($"[ConfigurationDescriptor] Using Dapr Configuration Provider. StoreName: {Option.DaprStoreName}");
        //     // TODO: 1) Consider JsonSerializer for configuration serialization storage. 2) Use a singleton DaprClient.
        //     var client = new DaprClientBuilder().Build();
        //     manager.AddDaprConfigurationStore(Option.DaprStoreName!, [], client,
        //         TimeSpan.FromSeconds(10));
        //     manager.AddStreamingDaprConfigurationStore(Option.DaprStoreName!, [], client,
        //         TimeSpan.FromSeconds(10));
        // }

        //use reflection to call AddOptions<T> and Bind
        var method = typeof(OptionsServiceCollectionExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m is { Name: "AddOptions", IsGenericMethod: true }).SingleOrDefault(m =>
            {
                var parameters = m.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(IServiceCollection);
            });

        if (method == null)
        {
            throw new InvalidOperationException("AddOptions<T> method is not found.");
        }

        _method = method;
    }
  
    public override void PostConfigureServices(IServiceCollection services)
    {
        // Important behavior: when option properties are List/Array and multiple configuration sources exist,
        // .NET appends elements instead of replacing them. This is by design. See dotnet/runtime #36384.
        ConfigurationRuntime.Setting.SetOtherSourceAction?.Invoke(ConfigurationRuntime.AppConfiguration);
        ConfigurationRegistration.RefreshProviders();
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            // Keep the full business type stream available for downstream modules such as auto-controllers,
            // localization, and other discovery-based features.
            if (type.IsClass && type.GetCustomAttribute<ConfigurationAttribute>(false) is
            {
                IsSubConfiguration: false
            }
            )
            {
                RegisterConfigurationType(type);
            }

            yield return type;
        }
    }

    private void RegisterConfigurationType(Type configType)
    {
        var card = new ConfigurationRegistration(configType);
        var provider = new LocalJsonFileProvider(card);
        provider.GenAndRegisterConfigurationFiles();
        ConfigurationRegistration.Register(card);

        var configAttr = card.Configuration.Info;
        Logger.LogDebug($"AddOptions<{configType.Name}>");
        dynamic optionsBuilder = _method.MakeGenericMethod(configType).Invoke(null, [_services])!;

        var configAction = new Action<BinderOptions>(o =>
        {
            o.ErrorOnUnknownConfiguration =
                configAttr.ErrorOnUnknownConfiguration ?? Option.ErrorOnUnknownConfiguration;
            o.BindNonPublicProperties = configAttr.BindNonPublicProperties ?? false;
        });
        if (configAttr.Section is { } section)
        {
            Logger.LogDebug($"Bind<{configType.Name}> to {section} (with section name)");
            ConfigurationOptionsBuilderExtensions.Bind(optionsBuilder, GetAppConfiguration().GetSection(section),
                configAction);
        }
        else
        {
            Logger.LogDebug($"Bind<{configType.Name}> (without section name)");
            ConfigurationOptionsBuilderExtensions.Bind(optionsBuilder, GetAppConfiguration(),
                configAction);
        }

        OptionsBuilderDataAnnotationsExtensions.ValidateDataAnnotations(optionsBuilder);
    }

    private IConfigurationManager GetAppConfiguration()
    {
        return Option.AppConfiguration ?? throw new InvalidOperationException(
            $"{nameof(ModuleConfigurationOption.AppConfiguration)} is not initialized. Register Monica with an IHostApplicationBuilder so the Configuration module can use builder.Configuration, or set a custom configuration manager explicitly.");
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            endpoints.MapGet(ConfigurationRoutes.DashboardConfigHistory,
                    async ([FromQuery] string? key, [FromQuery] string? appid, [FromQuery] DateTime? start,
                        [FromQuery] DateTime? end, [FromServices] ConfigurationFacade facade) =>
                    {
                        return (await facade.GetConfigHistoryAsync(key, appid, start, end)).GetResponse();
                    })
                .WithName("获取配置类历史");

            endpoints.MapPost(ConfigurationRoutes.DashboardConfigRollback,
                    async ([FromBody] ConfigurationRollbackRequest req, [FromServices] ConfigurationFacade facade) =>
                    {
                        return (await facade.RollbackConfigAsync(req.Key, req.AppId, req.Version)).GetResponse();
                    })
                .WithName("回滚配置类");

            endpoints.MapPost(ConfigurationRoutes.DashboardConfigUpdate, async (ConfigurationUpdateRequest req,
                    [FromServices] ConfigurationFacade facade) =>
                {
                    return (await facade.UpdateConfigAsync(req)).GetResponse();
                })
                .WithName("更新指定配置");

            endpoints.MapGet(ConfigurationRoutes.DashboardOptionItemStatus,
                    async ([FromQuery] string? appid, [FromQuery] string key,
                        [FromServices] ConfigurationFacade facade) =>
                    {
                        return (await facade.GetOptionItemAsync(appid, key)).GetResponse();
                    })
                .WithName("获取指定配置状态");

            endpoints.MapGet(ConfigurationRoutes.DashboardAllConfigStatus, async (
                    [FromServices] ConfigurationFacade facade,
                    [FromQuery] string? mode,
                    [FromQuery] bool onlyCurDomain = false) =>
                {
                    return (await facade.GetConfigsAsync(mode, onlyCurDomain)).GetResponse();
                })
                .WithName("获取所有微服务配置状态");
        });
    }

   
}

public class ModuleConfigurationGuide : WebModuleGuide<ModuleConfiguration, ModuleConfigurationOption, ModuleConfigurationGuide>
{
    /// <summary>
    /// Configures the history store used for configuration update and rollback records.
    /// </summary>
    public ModuleConfigurationGuide ConfigCustomStore<TStore>()
        where TStore : class, IConfigurationHistoryStore
    {
        ConfigureServices(context => { context.Services.AddTransient<IConfigurationHistoryStore, TStore>(); },
            ModuleRegistrationOrder.PreConfig);
        return this;
    }
}

public class ModuleConfigurationOption : MinimalApiModuleOptions<ModuleConfiguration>
{

    /// <summary>
    /// When false (the default), no exceptions are thrown when a configuration key is found for which the
    /// provided model object does not have an appropriate property which matches the key's name.
    /// When true, an <see cref="InvalidOperationException"/> is thrown with a description
    /// of the missing properties.
    /// </summary>
    /// <remarks>
    /// This checks whether every key in a given dictionary maps to a property on the target configuration type.
    /// It is intended for explicit source-to-type mapping rather than host-level configuration binding.
    /// </remarks>
    public bool ErrorOnUnknownConfiguration { get; set; }

    /// <summary>
    /// Throws when a configuration type is not annotated with <see cref="ConfigurationAttribute"/>.
    /// By default, this is disabled and only logs an error.
    /// </summary>
    public bool ErrorOnNoTagConfigAttribute { get; set; }
    /// <summary>
    /// Requires configuration properties to also use <see cref="OptionSettingAttribute"/>
    /// when <see cref="ConfigurationAttribute"/> is applied; otherwise throws an exception.
    /// </summary>
    public bool ErrorOnNoTagOptionAttribute { get; set; }

    /// <summary>
    /// Enables configuration-read logging.
    /// TODO: Not implemented yet; planned via dynamic setter interception.
    /// </summary>
    public bool EnableReadConfigLogging { get; set; }

    /// <summary>
    /// Enables configuration-registration logging.
    /// </summary>
    public bool EnableConfigRegisterLogging { get; set; }

    /// <summary>
    /// Gets or sets the application configuration manager used to bind discovered configuration types
    /// and append generated configuration sources.
    /// When not configured, the module uses <see cref="IHostApplicationBuilder.Configuration"/>.
    /// Set this only when the module should bind against a custom configuration manager.
    /// </summary>
    public IConfigurationManager? AppConfiguration { get; set; }

    /// <summary>
    /// Allows logging option values even when <see cref="OptionSettingAttribute"/> is missing.
    /// </summary>
    public bool EnableLoggingWithoutOptionSetting { get; set; }

    #region Configuration File Management

    /// <summary>
    /// Generates and manages configuration files per configuration type
    /// (created under the runtime path).
    /// </summary>
    public bool GenerateFileForEachOption { get; set; }

    /// <summary>
    /// Parent folder for generated configuration files.
    /// </summary>
    public string? GenerateOptionFileParentDirectory { get; set; } = "configs";
    
    /// <summary>
    /// Specifies how removed properties in configuration types are handled.
    /// </summary>
    public LocalJsonFileProvider.RemovedPropertyHandling RemovedPropertyHandling { get; set; } = LocalJsonFileProvider.RemovedPropertyHandling.Comment;
    #endregion

    /// <summary>
    /// Adds additional configuration sources with higher precedence.
    /// Precedence follows read order: later sources override earlier duplicate keys.
    /// Default read rules:
    /// <para></para>JsonDocumentOptions options = new JsonDocumentOptions()
    /// <para></para>{
    /// <para></para>  CommentHandling = JsonCommentHandling.Skip,
    /// <para></para>  AllowTrailingCommas = true
    /// <para></para>};
    /// </summary>
    public Action<IConfigurationManager>? SetOtherSourceAction { get; set; }
}
