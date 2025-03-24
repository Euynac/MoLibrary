using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.Utils;

namespace MoLibrary.DomainDrivenDesign.AutoController.Extensions;

public static class ActionDescriptorExtensions
{
    public static ControllerActionDescriptor AsControllerActionDescriptor(this ActionDescriptor actionDescriptor)
    {
        if (!actionDescriptor.IsControllerAction())
        {
            throw new Exception($"{nameof(actionDescriptor)} should be type of {typeof(ControllerActionDescriptor).AssemblyQualifiedName}");
        }

        return (actionDescriptor as ControllerActionDescriptor)!;
    }

    public static MethodInfo GetMethodInfo(this ActionDescriptor actionDescriptor)
    {
        return actionDescriptor.AsControllerActionDescriptor().MethodInfo;
    }

    public static Type GetReturnType(this ActionDescriptor actionDescriptor)
    {
        return actionDescriptor.GetMethodInfo().ReturnType;
    }

    public static bool HasObjectResult(this ActionDescriptor actionDescriptor)
    {
        return ActionResultHelper.IsObjectResult(actionDescriptor.GetReturnType());
    }

    public static bool IsControllerAction(this ActionDescriptor actionDescriptor)
    {
        return actionDescriptor is ControllerActionDescriptor;
    }

    public static bool IsPageAction(this ActionDescriptor actionDescriptor)
    {
        return actionDescriptor is PageActionDescriptor;
    }
    
    public static PageActionDescriptor AsPageAction(this ActionDescriptor actionDescriptor)
    {
        if (!actionDescriptor.IsPageAction())
        {
            throw new Exception($"{nameof(actionDescriptor)} should be type of {typeof(PageActionDescriptor).AssemblyQualifiedName}");
        }

        return (actionDescriptor as PageActionDescriptor)!;
    }
}

public static class ActionResultHelper
{
    public static List<Type> ObjectResultTypes { get; }

    static ActionResultHelper()
    {
        ObjectResultTypes = new List<Type>
        {
            typeof(JsonResult),
            typeof(ObjectResult),
            typeof(NoContentResult)
        };
    }

    public static bool IsObjectResult(Type returnType, params Type[] excludeTypes)
    {
        returnType = AsyncHelper.UnwrapTask(returnType);

        if (!excludeTypes.IsNullOrEmptySet() && excludeTypes.Any(t => t.IsAssignableFrom(returnType)))
        {
            return false;
        }

        if (!typeof(IActionResult).IsAssignableFrom(returnType))
        {
            return true;
        }

        return ObjectResultTypes.Any(t => t.IsAssignableFrom(returnType));
    }
}
