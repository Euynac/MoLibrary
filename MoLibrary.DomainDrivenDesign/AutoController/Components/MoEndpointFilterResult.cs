using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DomainDrivenDesign.AutoController.Components;

/// <summary>
/// 对minimal api适用的版本
/// </summary>
public class MoEndpointFilterResult : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);
        if (result is ObjectResult { Value: IMoResponse response } objResult)
        {
            context.HttpContext.Response.StatusCode =
                (int?) response.GetHttpStatusCode() ?? (int) HttpStatusCode.BadRequest;
            return objResult.Value;
            //巨坑：Minimal api的行为和mvc controller序列化行为不一样。mvc会对ObjectResult的value作为返回，而minimal api直接序列化了。
            //后续发现：minimal api应使用Microsoft.AspNetCore.Http.Results返回。可以使用Results.Json()替代ObjectResult.
        }

        return result;
    }
}
