using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;

namespace MoLibrary.Configuration.Dashboard.Modules;

/// <summary>
/// 配置管理UI模块扩展方法
/// </summary>
public static class ModuleConfigurationUIBuilderExtensions
{
    public static ModuleConfigurationUIGuide ConfigMoConfigurationUI(this WebApplicationBuilder builder,
        Action<ModuleConfigurationUIOption>? action = null)
    {
        return new ModuleConfigurationUIGuide().Register(action);
    }
}