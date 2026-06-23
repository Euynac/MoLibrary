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
/// <remarks>
/// CRUD participation is decided solely by the <see cref="ICrudApplicationService"/> marker interface. The
/// <see cref="CrudControllerOption.CrudControllerPostfix"/> is not a registration filter; it is used only to derive the
/// route name. As a diagnostic aid this provider logs an error when a registered CRUD service does not end with that
/// suffix (its route name would then be left unstripped), but it still registers the controller. Successful matches are
/// not logged to keep startup output quiet. Naming-convention enforcement itself lives in the project-unit system
/// (Monica.Framework).
/// </remarks>
/// <param name="logger">Logger used to report controllers whose name does not match the route suffix.</param>
/// <param name="options">CRUD controller options.</param>
public class CrudControllerFeatureProvider(
    ILogger<CrudControllerFeatureProvider> logger,
    IOptions<CrudControllerOption> options) : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo)
    {
        if (typeInfo is not { IsClass: true, IsGenericType: false } ||
            !typeInfo.AsType().IsImplementInterface<ICrudApplicationService>())
        {
            return false;
        }

        var postfix = options.Value.CrudControllerPostfix;
        if (!string.IsNullOrEmpty(postfix) && !typeInfo.Name.EndsWith(postfix))
        {
            logger.LogError(
                "CRUD controller '{ControllerName}' does not end with the configured route suffix '{RequiredSuffix}', so its route name will not be stripped.",
                typeInfo.Name,
                postfix);
        }

        return true;
    }
}
