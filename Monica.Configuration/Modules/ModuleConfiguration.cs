using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monica.Configuration;
using Monica.Configuration.Annotations;
using Monica.Configuration.Implements;
using Monica.Configuration.Interfaces;
using Monica.Configuration.Model;
using Monica.Configuration.Providers;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

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
public class ModuleConfiguration(ModuleConfigurationOption option) : ModuleBase<ModuleConfiguration, ModuleConfigurationOption, ModuleConfigurationGuide>(option), IBusinessTypeIterator
{
    private IServiceCollection _services = null!;
    private MethodInfo _method = null!;

    public override void ConfigureServices(IServiceCollection services)
    {
        _services = services;
        MoConfigurationManager.Setting = Option;
        MoConfigurationManager.AppConfiguration = Option.AppConfiguration;

        services.AddOptions();
        services.AddSingleton<IMoConfigurationCardManager, MoConfigurationCardManager>();
        services.TryAddSingleton<IMoProjectCatalog, ServiceDiscoveryProjectCatalog>(); // TODO: Decouple ServiceDiscovery dependency and define best-practice project layouts (monolith vs microservices).
        services.AddSingleton<IMoConfigurationServiceInfo, MoConfigurationServiceInfoDefault>();

        // if (Option is { UseDaprProvider: true, AppConfiguration: ConfigurationManager manager})
        // {
        //     Logger.LogDebug($"[MoConfiguration] Using Dapr Configuration Provider. StoreName: {Option.DaprStoreName}");
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
        MoConfigurationManager.Setting.SetOtherSourceAction?.Invoke((ConfigurationManager) MoConfigurationManager.AppConfiguration);
        MoConfigurationCard.RefreshProviders();
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var configType in types
                     .Where(x => x.IsClass && x.GetCustomAttribute<ConfigurationAttribute>(false) is
                     {
                         IsSubConfiguration: false
                     }))
        {
            var card = new MoConfigurationCard(configType);
            var provider = new LocalJsonFileProvider(card);
            provider.GenAndRegisterConfigurationFiles();
            MoConfigurationCard.Register(card);

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
                // OptionsBuilderConfigurationExtensions.Bind(optionsBuilder, Option.AppConfiguration.GetSection(section), configAction);
                MoExtendedOptionsBuilderConfigurationExtensions.Bind(optionsBuilder, Option.AppConfiguration.GetSection(section),
                    configAction);
            }
            else
            {
                Logger.LogDebug($"Bind<{configType.Name}> (without section name)");
                // OptionsBuilderConfigurationExtensions.Bind(optionsBuilder, Option.AppConfiguration, configAction);
                MoExtendedOptionsBuilderConfigurationExtensions.Bind(optionsBuilder, Option.AppConfiguration,
                    configAction);
            }

            OptionsBuilderDataAnnotationsExtensions.ValidateDataAnnotations(optionsBuilder);
            yield return configType;
        }
    }

   
}

public class ModuleConfigurationGuide : ModuleGuide<ModuleConfiguration, ModuleConfigurationOption, ModuleConfigurationGuide>
{

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
    /// Application configuration root.
    /// </summary>
    public IConfiguration AppConfiguration { get; set; } = null!;

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
    public Action<ConfigurationManager>? SetOtherSourceAction { get; set; }
}
