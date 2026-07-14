using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Monica.Tool.Extensions;
using Monica.Tool.Helpers;
using Monica.WebApi.AutoControllers.Abstractions;
using Monica.WebApi.AutoControllers.Abstractions.Internal;
using Monica.WebApi.AutoControllers.Models;

namespace Monica.WebApi.AutoControllers.Services.Support;

/// <summary>
/// Builds conventional routes for generated AutoController actions.
/// </summary>
/// <param name="httpMethodResolver">The current host's HTTP method convention resolver.</param>
public class ConventionalRouteBuilder(IConventionalHttpMethodResolver httpMethodResolver)
    : IConventionalRouteBuilder
{
    public virtual string Build(
        string rootPath,
        string controllerName,
        ActionModel action,
        string httpMethod,
        ConventionalControllerSetting? configuration)
    {

        // If rootPath is empty, a custom Route attribute already exists, so a relative path must be used for route composition.
        var url = rootPath.IsNullOrEmpty() ? $"{NormalizeControllerNameCase(controllerName, configuration)}" : $"{rootPath}/{NormalizeControllerNameCase(controllerName, configuration)}";

        //Add {id} path if needed
        var idParameterModel = action.Parameters.FirstOrDefault(p => p.ParameterName == "id");
        if (idParameterModel != null)
        {
            if (TypeHelper.IsPrimitiveExtended(idParameterModel.ParameterType, includeEnums: true))
            {
                url += "/{id}";
            }
            else
            {
                var properties = idParameterModel
                    .ParameterType
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public);

                foreach (var property in properties)
                {
                    url += "/{" + NormalizeIdPropertyNameCase(property, configuration) + "}";
                }
            }
        }

        //Add action name if needed
        var actionNameInUrl = NormalizeUrlActionName(rootPath, controllerName, action, httpMethod, configuration);
        if (!actionNameInUrl.IsNullOrEmpty())
        {
            url += $"/{NormalizeActionNameCase(actionNameInUrl, configuration)}";

            //Add secondary Id
            var secondaryIds = action.Parameters
                .Where(p => p.ParameterName.EndsWith("Id", StringComparison.Ordinal)).ToList();
            if (secondaryIds.Count == 1)
            {
                url += $"/{{{NormalizeSecondaryIdNameCase(secondaryIds[0], configuration)}}}";
            }
        }

        return url;
    }

    protected virtual string NormalizeUrlActionName(string rootPath, string controllerName, ActionModel action,
        string httpMethod, ConventionalControllerSetting? configuration)
    {
        var actionNameInUrl = httpMethodResolver
            .RemovePrefix(action.ActionName, httpMethod)
            .RemovePostFix("Async");

        return actionNameInUrl;
    }


    protected virtual string NormalizeControllerNameCase(string controllerName,
        ConventionalControllerSetting? configuration)
    {
        return controllerName.ToKebabCase();
    }

    protected virtual string NormalizeActionNameCase(string actionName,
        ConventionalControllerSetting? configuration)
    {
        return actionName.ToKebabCase();
    }

    protected virtual string NormalizeIdPropertyNameCase(PropertyInfo property,
        ConventionalControllerSetting? configuration)
    {
        return property.Name;
    }

    protected virtual string NormalizeSecondaryIdNameCase(ParameterModel secondaryId,
        ConventionalControllerSetting? configuration)
    {
        return secondaryId.ParameterName;
    }
}
