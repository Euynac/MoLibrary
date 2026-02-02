using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monica.Configuration.Annotations;
using Monica.Configuration.Implements;
using Monica.Configuration.Interfaces;
using Monica.Configuration.Model;
using Monica.Configuration.Providers;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;

namespace Monica.Configuration.Modules;

public static class ModuleConfigurationBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 Configuration 模块
        /// </summary>
        public static ModuleConfigurationGuide AddConfiguration(Action<ModuleConfigurationOption>? action = null)
        {
            return new ModuleConfigurationGuide().Register(action);
        }
    }
}

public class ModuleConfiguration(ModuleConfigurationOption option) : MoModule<ModuleConfiguration, ModuleConfigurationOption, ModuleConfigurationGuide>(option), IWantIterateBusinessTypes
{
    private IServiceCollection _services = null!;
    private MethodInfo _method = null!;
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Configuration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        _services = services;
        MoConfigurationManager.Setting = Option;
        MoConfigurationManager.AppConfiguration = Option.AppConfiguration;

        services.AddOptions();
        services.AddSingleton<IMoConfigurationCardManager, MoConfigurationCardManager>();
        services.TryAddSingleton<IMoProjectCatalog, RegisterCentreProjectCatalog>();
        services.AddSingleton<IMoConfigurationServiceInfo, MoConfigurationServiceInfoDefault>();

        // if (Option is { UseDaprProvider: true, AppConfiguration: ConfigurationManager manager})
        // {
        //     Logger.LogDebug($"[MoConfiguration] Using Dapr Configuration Provider. StoreName: {Option.DaprStoreName}");
        //     //TODO 1.考虑使用JsonSerializer进行配置序列化存储 2.使用单例DaprClient
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
        //巨坑：当Option的属性是List或Array等类型，有多个Configuration来源，那么这里面的元素会Append而不是替换。设计如此。dotnet/runtime #36384
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
            var card = new MoConfigurationCard(configType)
            {
                FromProjectName = configType.Assembly.GetName().Name ?? "Unknown",
            };
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

public class ModuleConfigurationGuide : MoModuleGuide<ModuleConfiguration, ModuleConfigurationOption, ModuleConfigurationGuide>
{

}

public class ModuleConfigurationOption : MoModuleOptionWithMinimalApi<ModuleConfiguration>
{

    /// <summary>
    /// When false (the default), no exceptions are thrown when a configuration key is found for which the
    /// provided model object does not have an appropriate property which matches the key's name.
    /// When true, an <see cref="InvalidOperationException"/> is thrown with a description
    /// of the missing properties.
    /// </summary>
    /// <remarks>
    /// 这用于检查是否给定Dictionary中的所有键都有指定配置类匹配的属性。所以不会用于HostConfiguration的配置，而是针对指定配置源映射指定配置类。
    /// </remarks>
    public bool ErrorOnUnknownConfiguration { get; set; }

    /// <summary>
    /// 如果配置类没有被<see cref="ConfigurationAttribute"/>标记，则抛出异常。默认不抛出异常，仅记录日志。
    /// </summary>
    public bool ErrorOnNoTagConfigAttribute { get; set; }
    /// <summary>
    /// 启用当使用<see cref="ConfigurationAttribute"/>时，其配置参数必须同时使用<see cref="OptionSettingAttribute"/>，否则抛出异常
    /// </summary>
    public bool ErrorOnNoTagOptionAttribute { get; set; }

    /// <summary>
    /// 开启配置读取日志
    /// TODO 暂未实现，拟通过动态注入set方法实现
    /// </summary>
    public bool EnableReadConfigLogging { get; set; }

    /// <summary>
    /// 开启配置注册日志
    /// </summary>
    public bool EnableConfigRegisterLogging { get; set; }

    /// <summary>
    /// 应用程序相关配置字典实例
    /// </summary>
    public IConfiguration AppConfiguration { get; set; } = null!;

    /// <summary>
    /// 是否允许在没有配置项特性的情况下对选项进行日志记录
    /// </summary>
    public bool EnableLoggingWithoutOptionSetting { get; set; }

    #region 配置文件管理

    /// <summary>
    /// 按照配置类生成配置文件进行管理（生成在程序运行路径下）
    /// </summary>
    public bool GenerateFileForEachOption { get; set; }

    /// <summary>
    /// 配置类生成配置文件的父级文件夹
    /// </summary>
    public string? GenerateOptionFileParentDirectory { get; set; } = "configs";
    
    /// <summary>
    /// 指定如何处理配置类中删除的属性
    /// </summary>
    public LocalJsonFileProvider.RemovedPropertyHandling RemovedPropertyHandling { get; set; } = LocalJsonFileProvider.RemovedPropertyHandling.Comment;
    #endregion


    /// <summary>
    /// 设置其他配置来源，优先级高。（优先级就是读取的顺序，后面的读取重复的会覆盖前面的配置）
    /// 默认读取规则：
    /// <para></para>JsonDocumentOptions options = new JsonDocumentOptions()
    /// <para></para>{
    /// <para></para>  CommentHandling = JsonCommentHandling.Skip,
    /// <para></para>  AllowTrailingCommas = true
    /// <para></para>};
    /// </summary>
    public Action<ConfigurationManager>? SetOtherSourceAction { get; set; }
}
