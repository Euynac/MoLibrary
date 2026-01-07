using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module.TypeFinder;

namespace MoLibrary.Core.Module;

public static class Mo
{
    /// <summary>
    /// 已加载的模块立即进行注册，如一些模块有需要在注册期间进行使用的，如Configuration、Logging模块等。注意对于有嵌套依赖的模块注册慎用，会使得后续的这些模块配置失效，因为已经被注册。
    /// </summary>
    /// <param name="builder"></param>
    public static void RegisterInstantly(WebApplicationBuilder builder)
    {
        MoModuleRegisterCentre.RegisterServices(builder);
    }

    public static class Options
    {
        /// <summary>
        /// 模块默认日志级别
        /// </summary>
        public static LogLevel DefaultModuleLogLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// 如果模块注册出现异常则禁用Module，而不是抛出异常。
        /// 当设置为 true 时，如果模块在注册过程中出现异常，系统将记录错误并禁用该模块，而不是抛出异常中断整个应用程序的启动。
        /// 被禁用的模块在应用程序的生命周期内将被完全跳过，不会调用其任何配置或初始化方法。
        /// </summary>
        public static bool DisableModuleIfHasException { get; set; }

        /// <summary>
        /// 启用模块系统初始化后输出模块执行状态报告日志
        /// </summary>
        public static bool EnableLoggingModuleSummary { get; set; }

        /// <summary>
        /// 设置含有Endpoints的模块的默认Swagger分组名称，默认情况下以模块名为分组名称。
        /// </summary>
        public static string? DefaultModuleApiGroupName { get; set; }

        /// <summary>
        /// 设置模块的默认Minimal Api禁用状态，默认情况下不禁用。
        /// </summary>
        public static bool? DefaultMinimalApiDisabled { get; set; }


        /// <summary>
        /// 全局类型查找器
        /// </summary>
        public static IDomainTypeFinder GlobalTypeFinder => _globalTypeFinder ?? new MoDomainTypeFinder(new ModuleCoreOptionTypeFinder());

        private static IDomainTypeFinder? _globalTypeFinder;
        public static void ConfigTypeFinder(Action<ModuleCoreOptionTypeFinder>? configure = null)
        {
            var option = new ModuleCoreOptionTypeFinder();
            configure?.Invoke(option);
            _globalTypeFinder = new MoDomainTypeFinder(option);
        }
    }
}