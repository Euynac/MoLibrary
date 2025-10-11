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
        if (res is IMoResponse serviceResponse)
        {
            return new ObjectResult(serviceResponse)
            {
                StatusCode = (int?)serviceResponse.GetHttpStatusCode()
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
        where T : IMoResponse
    {
        var res = await response as IMoResponse;
        return new ObjectResult(res)
        {
            StatusCode = (int?)res.GetHttpStatusCode()
        };
    }

    /// <summary>
    /// 封装为RESTful API response
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="response"></param>
    /// <param name="controller"></param>
    /// <returns></returns>
    public static ObjectResult GetResponse<T>(this T response, ControllerBase controller)
        where T : IMoResponse
    {
        return new ObjectResult(response)
        {
            StatusCode = (int?)response.GetHttpStatusCode()
        };
    }
}