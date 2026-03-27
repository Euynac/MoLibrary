using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Monica.Tool.Results;

namespace Monica.DomainDrivenDesign.AutoController.Components;

/// <summary>
/// Keeps the HTTP status code aligned with the <see cref="Res" /> status code.
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
