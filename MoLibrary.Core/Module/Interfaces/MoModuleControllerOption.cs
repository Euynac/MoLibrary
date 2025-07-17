using MoLibrary.Core.Module.BuilderWrapper;
using MoLibrary.Core.Module.ModuleController;

namespace MoLibrary.Core.Module.Interfaces;

public class MoModuleControllerOption<TModule> : MoModuleOption<TModule>, IMoModuleControllerOption where TModule : IMoModule
{
    public string? SwaggerTag { get; set; }

    public string RoutePrefix { get; set; } = typeof(TModule).Name;
    public bool DisableControllers { get; set; }

    public bool? IsVisibleInSwagger { get; set; }

    public string GetSwaggerGroupName() => SwaggerTag ?? ModuleCoreOption.DefaultModuleSwaggerGroupName ?? typeof(TModule).Name;
    public virtual string GetControllerRouteTemplate<TController>() where TController: MoModuleControllerBase
    {
        return $"{RoutePrefix}/{typeof(TController).Name}";
    }

    public virtual string GetRoute<TController>(string path) where TController: MoModuleControllerBase
    {
        var controllerRoute = GetControllerRouteTemplate<TController>();
        var trimmedPath = path?.TrimStart('/') ?? string.Empty;
        return string.IsNullOrEmpty(trimmedPath) 
            ? controllerRoute 
            : $"{controllerRoute}/{trimmedPath}";
    }
}