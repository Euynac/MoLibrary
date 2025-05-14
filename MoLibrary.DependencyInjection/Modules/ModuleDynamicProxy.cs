using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.DependencyInjection.Modules;

public class ModuleDynamicProxy(ModuleDynamicProxyOption option)
    : MoModule<ModuleDynamicProxy, ModuleDynamicProxyOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DynamicProxy;
    }
}