using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Configuration.Interfaces;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Configuration.Modules;

public class ModuleConfigurationGuide : MoModuleGuide<ModuleConfiguration, ModuleConfigurationOption, ModuleConfigurationGuide>
{

    /// <summary>
    /// 根据项目获取领域信息，用于完善微服务配置状态接口信息返回
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public ModuleConfigurationGuide AddMoConfigurationDomainInfo<T>() where T : class, IMoConfigurationServiceInfo
    {
        ConfigureServices(context =>
        {
            context.Services.Replace(ServiceDescriptor.Singleton<IMoConfigurationServiceInfo, T>());
        });
        return this;
    }
}