using System.Reflection;
using BuildingBlocksPlatform.Configuration.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlocksPlatform.Configuration;

public static class UtilsConfiguration
{
    /// <summary>
    /// 获取配置类标签
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public static ConfigurationAttribute? GetConfigAttribute(Type type)
    {
        //获取T的ConfigurationAttribute
        var configAttr = type.GetCustomAttribute<ConfigurationAttribute>();
        if (configAttr is {Section: null, DisableSection: false} attr)
        {
            attr.Section = type.Name;
        }

        if (configAttr != null) return configAttr;

        if (MoConfigurationManager.Setting.ErrorOnNoTagConfigAttribute)
        {
            throw new InvalidOperationException(
                $"Type {type.FullName} is not tagged with {typeof(ConfigurationAttribute)}.");
        }

        MoConfigurationManager.Logger.LogError("Type {0} is not tagged with {1}.", type.FullName, typeof(ConfigurationAttribute));

        return null;
    }
   
    /// <summary>
    /// 获取配置类标签
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static ConfigurationAttribute? GetConfigAttribute<T>() where T : class => GetConfigAttribute(typeof(T));

    /// <summary>
    /// 检查该类是否标记了配置类标签
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool HasConfigAttribute(Type type) => type.GetCustomAttribute<ConfigurationAttribute>() != null;

    /// <summary>
    /// 获取指定配置类实例。不建议直接使用该方法获取，无验证等功能。
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public static object? GetConfig(Type optionType)
    {
        var configAttr = GetConfigAttribute(optionType);
        if (configAttr == null) return null;
        return configAttr.Section != null
            ?
            //获取指定节点的配置
            MoConfigurationManager.AppConfiguration.GetSection(configAttr.Section).Get(optionType)
            : MoConfigurationManager.AppConfiguration.Get(optionType);
    }


    /// <summary>
    /// 获取指定配置类实例
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static object? GetConfig(Type configType, IServiceProvider provider) 
    {
        var optionGeneric = typeof(IOptionsSnapshot<>);
        var optionInterface = optionGeneric.MakeGenericType(configType);
        var config = provider.GetService(optionInterface);
        if (config != null)
        {
            var method = optionInterface.GetMethod("Get");
            var value = method?.Invoke(config, [null]);
            return value;


            //// To get IOption<>
            //var property = optionInterface.GetProperty("Value");
            //return property!.GetValue(config);
        }

        MoConfigurationManager.Logger.LogError("Type {FullName} is not a configuration class or not registered in services.",
            configType.FullName);
        return null;
    }
    /// <summary>
    /// 在依赖注入容器构建阶段获取指定配置类实例。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="service"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static T GetConfig<T>(IServiceCollection service) where T : class, new()
    {
        //TODO 参考ABP读取方式增加效率
        //service.GetConfiguration();
        var configAttr = GetConfigAttribute<T>();
        var provider = service.BuildServiceProvider();
        var config = provider.GetService<IOptions<T>>();
        if (config != null) return config.Value;

        MoConfigurationManager.Logger.LogError("Type {FullName} is not a configuration class or not registered in services.",
            typeof(T).FullName);
        return new T();
    }
}