using MoLibrary.Core.Module.BuilderWrapper;

namespace MoLibrary.Core.Module.Interfaces;

public class MoModuleControllerOption<TModule> : MoModuleOption<TModule>, IMoModuleControllerOption where TModule : IMoModule
{
    public string? SwaggerTag { get; set; }

    public string RoutePrefix { get; set; } = typeof(TModule).Name;
    public bool EnableControllers { get; set; }
    public string GetSwaggerGroupName() => SwaggerTag ?? ModuleCoreOption.DefaultModuleSwaggerGroupName ?? typeof(TModule).Name;
}