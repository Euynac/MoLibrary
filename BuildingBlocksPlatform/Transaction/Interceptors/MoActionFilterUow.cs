using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.Transaction.Interceptors;

public class MoActionFilterUow : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor)
        {
            await next();
            return;
        }

        var unitOfWorkManager = context.HttpContext.RequestServices.GetRequiredService<IMoUnitOfWorkManager>();
        using var uow = unitOfWorkManager.Begin(new MoUnitOfWorkOptions());
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
