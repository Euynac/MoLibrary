using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Monica.Tool.Extensions;
using Monica.WebApi.AutoControllers.Abstractions;

namespace Monica.WebApi.AutoControllers.Services.Support;

/// <summary>
/// Determines which application services should be exposed as generated CRUD controllers.
/// </summary>
/// <remarks>
/// CRUD participation is decided solely by the <see cref="ICrudApplicationService"/> marker interface. The
/// route name. Suffix diagnostics run once during Monica type discovery; MVC feature evaluation remains pure and may
/// be invoked repeatedly by ASP.NET Core without producing duplicate diagnostics.
/// </remarks>
public class CrudControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo)
    {
        if (typeInfo is not { IsClass: true, IsGenericType: false } ||
            !typeInfo.AsType().IsImplementInterface<ICrudApplicationService>())
        {
            return false;
        }

        return true;
    }
}
