using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.DomainDrivenDesign.AutoController.Settings;
using Monica.DomainDrivenDesign.AutoCrud;
using Monica.Tool.Extensions;

namespace Monica.DomainDrivenDesign.AutoController.Features;

/// <summary>
/// Determines which application services should be exposed as generated CRUD controllers.
/// </summary>
/// <param name="logger">Logger used to report controller registration decisions.</param>
/// <param name="options">CRUD controller options.</param>
public class MoConventionalCrudControllerFeatureProvider(ILogger<MoConventionalCrudControllerFeatureProvider> logger, IOptions<MoCrudControllerOption> options) : ControllerFeatureProvider
{
    //private static int SearchTimes = 0;
    protected override bool IsController(TypeInfo typeInfo)
    {
        //SearchTimes++;
        //logger.LogInformation(SearchTimes.ToString());

        if (typeInfo is {IsClass: true, IsGenericType: false} && typeInfo.AsType().IsImplementInterface<IMoCrudAppService>())
        {
            if (typeInfo.Name.EndsWith(options.Value.CrudControllerPostfix))
            {
                logger.LogInformation("Automatically registered CRUD controller: {ControllerName}",
                    typeInfo.Name);
            }
            else
            {
                logger.LogError(
                    "Failed to auto-register CRUD controller '{ControllerName}' because it does not match the required suffix '{RequiredSuffix}'.",
                    typeInfo.Name,
                    options.Value.CrudControllerPostfix);
            }
            return true;
        }
        return false;
    }
}
