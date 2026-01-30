using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Monica.DomainDrivenDesign.AutoController.Settings;

namespace Monica.DomainDrivenDesign.AutoController.Interfaces;

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
