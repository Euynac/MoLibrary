using BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Settings;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Interfaces;

public interface IMoConventionalRouteBuilder
{
    string Build(
        string rootPath,
        string controllerName,
        ActionModel action,
        string httpMethod,
        ConventionalControllerSetting? configuration
    );
}
