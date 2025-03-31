using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Reflection;

namespace MoLibrary.Core.Module.ModuleController;

/// <summary>
/// Feature provider for controllers in a module.
/// </summary>
/// <typeparam name="TModuleController"></typeparam>
/// <param name="options"></param>
public class ModuleControllerFeatureProvider<TModuleController>(IMoModuleOptions options) : IApplicationFeatureProvider<ControllerFeature> where TModuleController : ControllerBase
{
    public virtual void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (!options.EnableControllers) return;

        foreach (var type in typeof(TModuleController).Assembly.GetTypes())
        {
            if (typeof(ControllerBase).IsAssignableFrom(type) && !feature.Controllers.Contains(type.GetTypeInfo()))
            {
                feature.Controllers.Add(type.GetTypeInfo());
            }
        }
    }
}