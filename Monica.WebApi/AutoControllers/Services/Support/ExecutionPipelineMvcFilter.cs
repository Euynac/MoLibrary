using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Monica.Core.Execution;
using Monica.WebApi.AutoControllers.Annotations;
using Monica.WebApi.AutoControllers.Models;

namespace Monica.WebApi.AutoControllers.Services.Support;

/// <summary>
/// Adapts direct MVC actions into Monica's shared execution pipeline while leaving mediated actions untouched.
/// </summary>
internal sealed class ExecutionPipelineMvcFilter(IExecutionPipeline executionPipeline) : IAsyncActionFilter
{
    private static readonly ConcurrentDictionary<(Type ControllerType, string ActionId), ExecutionDescriptor>
        DESCRIPTORS = new();

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor actionDescriptor)
        {
            await next().ConfigureAwait(false);
            return;
        }

        if (context.Controller.GetType().IsDefined(typeof(MediatedControllerAttribute), inherit: false)
            || actionDescriptor.MethodInfo.IsDefined(typeof(MediatedControllerAttribute), inherit: false))
        {
            await next().ConfigureAwait(false);
            return;
        }

        var controllerType = context.Controller.GetType();
        var descriptor = DESCRIPTORS.GetOrAdd(
            (controllerType, actionDescriptor.Id),
            _ => CreateDescriptor(controllerType, actionDescriptor));
        var input = new MvcActionExecutionInput(
            context.HttpContext,
            context.Controller,
            actionDescriptor,
            context.ActionArguments);
        var executionContext = new ExecutionContext<MvcActionExecutionInput>(
            descriptor,
            input,
            context.Controller,
            context.HttpContext.RequestAborted);

        var result = await executionPipeline.ExecuteAsync(
            executionContext,
            async () => CreateResult(await next().ConfigureAwait(false))).ConfigureAwait(false);

        if (result.ExecutedContext is null)
        {
            context.Result = result.Result;
        }
    }

    private static MvcActionExecutionResult CreateResult(ActionExecutedContext context)
    {
        if (context.Exception is not null && !context.ExceptionHandled)
        {
            ExceptionDispatchInfo.Capture(context.Exception).Throw();
        }

        return MvcActionExecutionResult.FromExecutedAction(context);
    }

    private static ExecutionDescriptor CreateDescriptor(
        Type controllerType,
        ControllerActionDescriptor actionDescriptor)
    {
        return new ExecutionDescriptor(
            MvcExecutionPoints.Action,
            $"{controllerType.FullName}.{actionDescriptor.MethodInfo.Name}",
            controllerType,
            actionDescriptor.MethodInfo,
            typeof(MvcActionExecutionInput),
            typeof(MvcActionExecutionResult),
            isBusinessOperation: true,
            isLongRunning: false);
    }
}
