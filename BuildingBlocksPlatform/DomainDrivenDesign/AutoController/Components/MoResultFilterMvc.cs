using System.Net;
using BuildingBlocksPlatform.SeedWork;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MoLibrary.Tool.MoResponse;

namespace BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Components;

/// <summary>
/// 使得Res的Status code与Http响应的Code一致。
/// </summary>
public class MoResultFilterMvc: IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: IServiceResponse response } && !response.IsOk())
        {
            context.HttpContext.Response.StatusCode =
                (int?) response.GetHttpStatusCode() ?? (int) HttpStatusCode.BadRequest;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
        
    }
}