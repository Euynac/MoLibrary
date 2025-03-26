using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Core.Extensions;

public static class ConfigExtensions
{
    /// <summary>
    /// 辅助设置配置
    /// </summary>
    /// <typeparam name="TConfig"></typeparam>
    /// <param name="services"></param>
    /// <param name="configAction"></param>
    public static void ConfigActionWrapper<TConfig>(this IServiceCollection services, Action<TConfig>? configAction)
        where TConfig : class, new()
    {
        ConfigActionWrapper(services, configAction, out _);
    }
    /// <summary>
    /// 辅助设置配置
    /// </summary>
    /// <typeparam name="TConfig"></typeparam>
    /// <param name="services"></param>
    /// <param name="configAction"></param>
    /// <param name="tmpConfig"></param>
    public static void ConfigActionWrapper<TConfig>(this IServiceCollection services, Action<TConfig>? configAction, out TConfig tmpConfig)
        where TConfig : class, new()
    {
        tmpConfig = new TConfig();
        configAction?.Invoke(tmpConfig);
        if (configAction != null)
        {
            services.Configure(configAction);
        }
    }
}