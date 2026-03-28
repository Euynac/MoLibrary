using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Monica.Core.Results;

namespace Monica.DomainDrivenDesign.AutoController.Components;

/// <summary>
/// Endpoint filter variant for Minimal API endpoints.
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

            // Minimal API serializes ObjectResult differently from MVC controllers.
            // MVC returns ObjectResult.Value, while Minimal API would serialize the ObjectResult
            // wrapper itself. Returning Value keeps the payload aligned with MVC behavior.
            return objResult.Value;
        }

        return result;
    }
}
