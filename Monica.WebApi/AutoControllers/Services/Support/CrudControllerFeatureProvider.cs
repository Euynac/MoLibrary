using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Tool.Extensions;
using Monica.WebApi.AutoControllers.Abstractions;
using Monica.WebApi.AutoControllers.Models;

namespace Monica.WebApi.AutoControllers.Services.Support;

/// <summary>
/// Determines which application services should be exposed as generated CRUD controllers.
/// </summary>
/// <param name="logger">Logger used to report controller registration decisions.</param>
/// <param name="options">CRUD controller options.</param>
public class CrudControllerFeatureProvider(ILogger<CrudControllerFeatureProvider> logger, IOptions<CrudControllerOption> options) : ControllerFeatureProvider
{
    //private static int SearchTimes = 0;
    protected override bool IsController(TypeInfo typeInfo)
    {
        //SearchTimes++;
        //logger.LogInformation(SearchTimes.ToString());

        if (typeInfo is {IsClass: true, IsGenericType: false} && typeInfo.AsType().IsImplementInterface<ICrudAppService>())
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
