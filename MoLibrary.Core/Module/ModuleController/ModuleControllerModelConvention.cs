using Microsoft.AspNetCore.Mvc.ApplicationModels;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Core.Module.ModuleController;

public class ModuleControllerModelConvention<TModuleController>(IMoModuleControllerOption options) : IApplicationModelConvention where TModuleController : MoModuleControllerBase
{
    public void Apply(ApplicationModel application)
    {
        if (options.GetIsControllerDisabled()) return;

        foreach (var controller in application.Controllers)
        {
            // 识别库中的Controller
            if (controller.ControllerType.Assembly == typeof(TModuleController).Assembly)
            {
                // 应用路由前缀
                foreach (var selector in controller.Selectors)
                {
                    selector.AttributeRouteModel = new AttributeRouteModel
                    {
                        Template = options.GetControllerRouteTemplate<TModuleController>()
                    };
                }

                // 设置Swagger标签
                controller.ApiExplorer.GroupName = options.GetSwaggerGroupName();
                controller.ApiExplorer.IsVisible = options.GetIsVisibleInSwagger();
            }
        }
    }
}