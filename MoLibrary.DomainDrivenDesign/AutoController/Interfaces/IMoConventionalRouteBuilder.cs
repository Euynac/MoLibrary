using Microsoft.AspNetCore.Mvc.ApplicationModels;
using MoLibrary.DomainDrivenDesign.AutoController.Settings;

namespace MoLibrary.DomainDrivenDesign.AutoController.Interfaces;

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
