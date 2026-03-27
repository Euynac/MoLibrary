using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Monica.Tool.Results;

namespace Monica.DomainDrivenDesign.AutoController.Components;

/// <summary>
/// 使得Res的Status code与Http响应的Code一致。
/// </summary>
public class MoResultFilterMvc: IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: IResultEnvelope response } && !response.IsOk())
        {
            context.HttpContext.Response.StatusCode =
                (int?) response.ToHttpStatusCode() ?? (int) HttpStatusCode.BadRequest;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
        
    }
}