using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Monica.Repository.UnitOfWork.Abstractions;

namespace Monica.Repository.UnitOfWork.Services.Support;

public class UnitOfWorkActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor)
        {
            await next();
            return;
        }

        var unitOfWorkManager = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWorkManager>();
        await using var uow = unitOfWorkManager.BeginScope();
        var result = await next();
        if (Succeed(result))
        {
            await uow.CompleteAsync(context.HttpContext.RequestAborted);
        }
        else
        {
            await uow.RollbackAsync(context.HttpContext.RequestAborted);
        }
    }


    private static bool Succeed(ActionExecutedContext result)
    {
        return result.Exception == null || result.ExceptionHandled;
    }
}
