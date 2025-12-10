using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.ObservableInstance;

namespace MoLibrary.Core.Modules;

/// <summary>
/// ObservableInstance module provides universal state and exception tracking for all instances.
/// Replaces ModuleExceptionPool functionality with a unified observable pattern.
/// </summary>
public class ModuleObservableInstance(ModuleObservableInstanceOption option)
    : MoModule<ModuleObservableInstance, ModuleObservableInstanceOption, ModuleObservableInstanceGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.ObservableInstance;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
     
        // Register ObservableInstance manager as singleton
        services.AddSingleton<IObservableInstanceManager, ObservableInstanceManager>();
    }
}
