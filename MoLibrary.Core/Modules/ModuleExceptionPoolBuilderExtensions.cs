using Microsoft.AspNetCore.Builder;

namespace MoLibrary.Core.Modules;

/// <summary>
/// 异常池模块扩展方法
/// </summary>
public static class ModuleExceptionPoolBuilderExtensions
{
    /// <summary>
    /// 配置异常池模块
    /// </summary>
    /// <param name="builder">应用程序构建器</param>
    /// <param name="action">配置选项的委托</param>
    /// <returns>异常池模块配置引导实例</returns>
    public static ModuleExceptionPoolGuide ConfigModuleExceptionPool(
        this WebApplicationBuilder builder,
        Action<ModuleExceptionPoolOption>? action = null)
    {
        return new ModuleExceptionPoolGuide().Register(action);
    }
}
