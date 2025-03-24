using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Extensions;

public static class RESTfulApiExtensions
{
    /// <summary>
    /// 封装为RESTful API response
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="response"></param>
    /// <param name="controller"></param>
    /// <returns></returns>
    public static async Task<object> GetResponse(this Task<object> response, ControllerBase controller)
    {
        var res = await response;
        if(res is IServiceResponse serviceResponse)
        {
            return new ObjectResult(serviceResponse)
            {
                StatusCode = (int?) serviceResponse.GetHttpStatusCode()
            };
        }
        return res;
    }
    /// <summary>
    /// 封装为RESTful API response
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="response"></param>
    /// <param name="controller"></param>
    /// <returns></returns>
    public static async Task<ObjectResult> GetResponse<T>(this Task<T> response, ControllerBase controller)
        where T : IServiceResponse
    {
        var res = await response as IServiceResponse;
        return new ObjectResult(res)
        {
            StatusCode = (int?)res.GetHttpStatusCode()
        };
    }
    /// <summary>
    /// 封装为RESTful API response
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="responses"></param>
    /// <param name="controller"></param>
    /// <returns></returns>
    public static async Task<ObjectResult> GetResponse<T>(this Task<List<Res<T>>> responses, ControllerBase controller)
    {
        var result = await responses;
        var res = result.ToBulkRes();
        return res.IsOk() ? controller.Ok(res) : controller.BadRequest(res);
    }
    /// <summary>Bulk Asynchronously send a request to a single handler</summary>
    /// <typeparam name="TResponse">Response type</typeparam>
    /// <param name="sender"></param>
    /// <param name="requests">Request object</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>A task that represents the send operation. The task result contains the handler response</returns>
    public static async Task<List<TResponse>> SendBulk<TResponse>(
        this ISender sender, IRequest<TResponse>[] requests,
        CancellationToken cancellationToken = default) 
    {
        var list = new List<TResponse>();
        foreach (var request in requests)
        {
            list.Add(await sender.Send(request, cancellationToken));
        }
        return list;
    }
}