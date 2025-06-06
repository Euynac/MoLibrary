using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.DependencyInjection.Modules;

public class ModuleDynamicProxy(ModuleDynamicProxyOption option)
    : MoModule<ModuleDynamicProxy, ModuleDynamicProxyOption, ModuleDynamicProxyGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DynamicProxy;
    }
}