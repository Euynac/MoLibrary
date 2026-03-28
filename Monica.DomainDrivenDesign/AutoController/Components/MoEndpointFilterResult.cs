using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Monica.Core.Results;

namespace Monica.DomainDrivenDesign.AutoController.Components;

/// <summary>
/// Endpoint filter variant for Minimal API.
/// </summary>
public class MoEndpointFilterResult : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);
        if (result is ObjectResult { Value: IResultEnvelope response } objResult)
        {
            context.HttpContext.Response.StatusCode =
                (int?) response.ToHttpStatusCode() ?? (int) HttpStatusCode.BadRequest;
            return objResult.Value;
            // Important: Minimal API serializes differently from MVC controllers.
            // MVC returns ObjectResult.Value, while Minimal API serializes the ObjectResult itself.
            // Later note: Minimal API should return Microsoft.AspNetCore.Http.Results; Results.Json() can replace ObjectResult.
        }

        return result;
    }
}
