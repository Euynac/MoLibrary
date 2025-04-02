using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace MoLibrary.Core.Module.ModuleController;

public class ModuleControllerModelConvention<TModuleController>(IMoModuleOptions options) : IApplicationModelConvention where TModuleController : MoModuleControllerBase
{
    public void Apply(ApplicationModel application)
    {
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
                        Template = $"{options.RoutePrefix}/{controller.ControllerName}"
                    };
                }

                // 设置Swagger标签
                controller.ApiExplorer.GroupName = options.SwaggerTag;
            }
        }
    }
}