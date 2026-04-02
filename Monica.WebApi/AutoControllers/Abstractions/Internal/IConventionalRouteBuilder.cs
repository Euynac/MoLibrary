using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Monica.WebApi.AutoControllers.Models;

namespace Monica.WebApi.AutoControllers.Abstractions.Internal;

public interface IConventionalRouteBuilder
{
    string Build(
        string rootPath,
        string controllerName,
        ActionModel action,
        string httpMethod,
        ConventionalControllerSetting? configuration
    );
}
