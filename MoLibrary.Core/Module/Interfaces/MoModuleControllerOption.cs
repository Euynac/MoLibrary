using MoLibrary.Core.Module.BuilderWrapper;
using MoLibrary.Core.Module.ModuleController;

namespace MoLibrary.Core.Module.Interfaces;

public class MoModuleControllerOption<TModule> : MoModuleOption<TModule>, IMoModuleControllerOption where TModule : IMoModule
{
    /// <summary>
    /// 设置该Api Group注意要与Swagger Tag区分，默认是显示Swagger Tag的。需要增加Filter自动转换。
    /// </summary>
    public string? ApiGroup { get; set; }

    public string? RoutePrefix { get; set; }
    public bool? IsControllerDisabled { get; set; }

    public bool? IsVisibleInSwagger { get; set; }

    public string GetApiGroupName() => ApiGroup ?? ModuleCoreOption.DefaultModuleApiGroupName ?? typeof(TModule).Name;


    public bool GetIsControllerDisabled() => IsControllerDisabled ?? ModuleCoreOption.DefaultDisableControllers ?? false;

    public bool GetIsVisibleInSwagger() => IsVisibleInSwagger ?? ModuleCoreOption.DefaultIsVisibleInSwagger ?? true;

    public virtual string GetControllerRouteTemplate<TController>() where TController: MoModuleControllerBase
    {
        return RoutePrefix ?? ModuleCoreOption.DefaultRoutePrefix ?? $"{typeof(TModule).Name}/{typeof(TController).Name}";
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