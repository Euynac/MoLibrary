using BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Extensions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.DomainDrivenDesign.ExceptionHandler;

//TODO 和GlobalExceptionHandler关系？是否多余？
public class MoMvcExceptionFilter : IAsyncExceptionFilter, ITransientDependency
{
    public virtual async Task OnExceptionAsync(ExceptionContext context)
    {
        if (!ShouldHandleException(context))
        {
            LogException(context);
            return;
        }

        await HandleAndWrapException(context);
    }

    protected virtual bool ShouldHandleException(ExceptionContext context)
    {
        if (context.ActionDescriptor.IsControllerAction() &&
            context.ActionDescriptor.HasObjectResult())
        {
            return true;
        }

        return false;
    }

    protected virtual async Task HandleAndWrapException(ExceptionContext context)
    {
        LogException(context);

        //else
        //{
        //    context.HttpContext.Response.Headers[AbpHttpConsts.AbpErrorFormat] = "true";
        //    context.HttpContext.Response.StatusCode = (int)context
        //        .GetRequiredService<IHttpExceptionStatusCodeFinder>()
        //        .GetStatusCode(context.HttpContext, context.Exception);
        //    var res = Res.CreateError(remoteServiceErrorInfo, "接口出现异常",
        //        ResponseCode.InternalError);
        //    context.Result = new ObjectResult(res);
        //    context.Exception = null!; //Handled!
        //}


    }

    protected virtual void LogException(ExceptionContext context)
    {
        var logger = context.HttpContext.RequestServices.GetService<ILogger<MoMvcExceptionFilter>>();
        logger?.LogError(context.Exception, $"{context.ActionDescriptor.DisplayName} threw an exception");
    }
}