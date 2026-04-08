using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Configuration.Annotations;

namespace Monica.Configuration.Services.Support;

public static class ConfigurationTypeAccessor
{
    /// <summary>
    /// Gets the configuration attribute for the specified type.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public static ConfigurationAttribute? GetConfigAttribute(Type type)
    {
        // Read ConfigurationAttribute from the target type.
        var configAttr = type.GetCustomAttribute<ConfigurationAttribute>();
        if (configAttr is {Section: null, DisableSection: false} attr)
        {
            attr.Section = type.Name;
        }

        if (configAttr != null) return configAttr;

        if (ConfigurationRuntime.Setting.ErrorOnNoTagConfigAttribute)
        {
            throw new InvalidOperationException(
                $"Type {type.FullName} is not tagged with {typeof(ConfigurationAttribute)}.");
        }

        ConfigurationRuntime.Logger.LogError("Type {0} is not tagged with {1}.", type.FullName, typeof(ConfigurationAttribute));

        return null;
    }
   
    /// <summary>
    /// Gets the configuration attribute for type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static ConfigurationAttribute? GetConfigAttribute<T>() where T : class => GetConfigAttribute(typeof(T));

    /// <summary>
    /// Checks whether the type is annotated with <see cref="ConfigurationAttribute"/>.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool HasConfigAttribute(Type type) => type.GetCustomAttribute<ConfigurationAttribute>() != null;

    /// <summary>
    /// Gets an instance of the specified configuration type.
    /// Prefer using the options pipeline because this method does not include validation.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public static object? GetConfig(Type optionType)
    {
        var configAttr = GetConfigAttribute(optionType);
        if (configAttr == null) return null;
        return configAttr.Section != null
            ?
            // Read configuration from the specified section.
            ConfigurationRuntime.AppConfiguration.GetSection(configAttr.Section).Get(optionType)
            : ConfigurationRuntime.AppConfiguration.Get(optionType);
    }


    /// <summary>
    /// Gets an instance of the specified configuration type from the service provider.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static object? GetConfig(Type configType, IServiceProvider provider)
    {
        var optionGeneric = typeof(IOptionsMonitor<>);
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

        ConfigurationRuntime.Logger.LogError("Type {FullName} is not a configuration class or not registered in services.",
            configType.FullName);
        return null;
    }
    /// <summary>
    /// Gets an instance of the specified configuration type during service collection build-up.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="service"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static T GetConfig<T>(IServiceCollection service) where T : class, new()
    {
        // TODO: Improve efficiency by adopting an ABP-style access approach.
        //service.GetConfiguration();
        var configAttr = GetConfigAttribute<T>();
        var provider = service.BuildServiceProvider();
        var config = provider.GetService<IOptions<T>>();
        if (config != null) return config.Value;

        ConfigurationRuntime.Logger.LogError("Type {FullName} is not a configuration class or not registered in services.",
            typeof(T).FullName);
        return new T();
    }
}
