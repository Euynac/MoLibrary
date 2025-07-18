using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Core.Module.ModuleController;

public class ConditionalControllerFeatureProvider(HashSet<Type> enabledTypes)
    : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        var toRemove = feature.Controllers
            .Where(c => !enabledTypes.Contains(c) && c.IsSubclassOf(typeof(MoModuleControllerBase)))
            .ToList();
        feature.Controllers.RemoveAll(toRemove);
    }
}