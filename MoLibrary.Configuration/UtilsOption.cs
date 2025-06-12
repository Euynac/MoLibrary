using Microsoft.Extensions.Logging;
using MoLibrary.Configuration.Annotations;
using MoLibrary.Configuration.Model;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.General;

namespace MoLibrary.Configuration;

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
    /// 对指定配置类实例当前取值进行日志记录。使用 <see cref="OptionSettingAttribute"/> 中的LoggingFormat或Description进行格式化。
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
     
        //对T的每个属性获取OptionsAttribute
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