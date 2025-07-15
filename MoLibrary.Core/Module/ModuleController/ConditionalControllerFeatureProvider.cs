using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace MoLibrary.Core.Module.ModuleController;

public class ConditionalControllerFeatureProvider(Func<TypeInfo, bool> predicate)
    : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        var toRemove = feature.Controllers
            .Where(c => !predicate(c))
            .ToList();

        foreach (var controller in toRemove)
        {
            feature.Controllers.Remove(controller);
        }
    }
}