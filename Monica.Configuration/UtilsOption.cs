using Microsoft.Extensions.Logging;
using Monica.Configuration.Annotations;
using Monica.Configuration.Model;
using Monica.Tool.Diagnostics;
using Monica.Tool.Extensions;

namespace Monica.Configuration;

public static class UtilsOption
{
    
    internal static string GetOptionItemString(Type config, object? configInstance)
    {
        return new MoConfiguration(config, configInstance).ToString();
    }

    internal static string GetOptionItemString(Type config)
    {
        var configInstance = UtilsConfiguration.GetConfig(config);
        return GetOptionItemString(config, configInstance);
       
    }
    /// <summary>
    /// Logs the current values of the specified configuration instance.
    /// Uses <see cref="OptionSettingAttribute"/>.LoggingFormat or Description for formatting.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="config"></param>
    public static void LogCurrentConfig<T>(this T? config) where T : class
    {
        if (config == null)
        {
            MoConfigurationManager.Logger.LogWarning("Option {FullName} is null.", typeof(T).GetCleanFullName());
            return;
        }

        var configInfo = MoConfiguration.Create(config);
     
        // Get OptionSettingAttribute metadata for each property of T.
        var items = configInfo.OptionItems.Select(p => p);
        foreach (var option in items)
        {
            var value = option.Value;
            
            var displayValue = option.Value;
            if (value != null && value.GetType().IsClass && value.GetType() != typeof(string))
            {
                displayValue = value.ToJsonString();
            }
            
            if (option.Info is not { } optionAttr)
            {
                MoConfigurationManager.Logger.LogInformation("[{0}] {1}: {2}", typeof(T).Name, option.Name, displayValue);
            }
            else if (optionAttr.LoggingFormat is { } format)
            {
                MoConfigurationManager.Logger.LogInformation(format, displayValue);
            }
            else if (optionAttr.Title is { } description)
            {
                MoConfigurationManager.Logger.LogInformation("[{0}] {1}: {2}", typeof(T).Name, description, displayValue);
            }
        }
    }
}
