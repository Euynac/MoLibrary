using BuildingBlocksPlatform.Converters;
using BuildingBlocksPlatform.SeedWork;
using MoLibrary.Tool.General;
using MediatR;
using Microsoft.AspNetCore.Http.Extensions;
using MoLibrary.Tool.MoResponse;

namespace BuildingBlocksPlatform.Extensions;

public static class HttpApiExtensions
{
    /// <summary>
    /// 统一获取内部微服务调用API响应
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="response">需要检查 IsSuccess 属性是否是 <b>true</b>，否则 <typeparamref name="TResponse"/> 的属性全为null或默认值，非有效值</param>
    /// <returns></returns>
    public static async Task<TResponse> GetResponse<TResponse>(this Task<HttpResponseMessage> response)
        where TResponse : class, IServiceResponse, new()
    {
        var resContent = string.Empty;
        HttpResponseMessage? httpResponse = null;
        Exception? e = null;
        TResponse? res = default;
        try
        {
            httpResponse = await response;
            resContent = await httpResponse.Content.ReadAsStringAsync();
            res = JsonSerializer.Deserialize<TResponse>(resContent, JsonShared.GlobalJsonSerializerOptions);
            res?.AutoParseResponseFromOrigin(resContent);
            if (res?.IsServiceNormal() is true)
            {
                return res;
            }
        }
        catch (Exception ex)
        {
            e = ex;
        }

        var errorRes = new TResponse
        {
            Code = ResponseCode.InternalError,
            Message = "接口响应出错",
        };

        if (e != null)
        {
            if (e is JsonException jsonEx)
            {
                errorRes.AppendExtraInfo("jsonException", jsonEx.GetJsonErrorDetails(resContent));
            }
            else
            {
                errorRes.AppendExtraInfo("exception", e.ToString());
            }
            var innerException = e.InnerException;
            while (innerException != null)
            {
                errorRes.AppendExtraInfo("exception", innerException.ToString());
                innerException = innerException.InnerException;
            }
        }

        object? contentJson;
        try
        {
            contentJson = res is not null ? null : string.IsNullOrWhiteSpace(resContent) ? "<Empty>" : JsonSerializer.Deserialize<object>(resContent);
        }
        catch
        {
            contentJson = resContent;
        }

        errorRes.AppendExtraInfo("response", new
        {
            Content = res,
            contentJson,
            ResponseType = typeof(TResponse).GetGenericTypeName(),
            Header = httpResponse?.Headers.ToString(),
            StatusCode = httpResponse?.StatusCode.ToString(),
            httpResponse?.ReasonPhrase,
        });


        if (httpResponse is { IsSuccessStatusCode: false })
        {
            errorRes.AppendExtraInfo("request", await FormatSource(httpResponse));
        }

        GlobalLog.LogError($"{httpResponse?.RequestMessage?.RequestUri}接口响应出错：{errorRes.ToJsonStringForce()}");

        return errorRes;


        static async Task<object> FormatSource(HttpResponseMessage httpResponse)
        {
            var content = httpResponse.RequestMessage?.Content is { } httpContent ? await httpContent.ReadAsStringAsync() : null;

            return new
            {
                RequestMsg = httpResponse.RequestMessage?.ToString(),
                Content = content,
                httpResponse.RequestMessage?.RequestUri
            };
        }
    }
   
    public static string ToQueryString<T>(this T request) where T : class, IBaseRequest
    {
        var builder = new QueryBuilder();
        foreach (var property in request.GetType().GetProperties().Where(p => p.CanRead))
        {
            var value = property.GetValue(request);
            if (value != null)
            {
                builder.Add(property.Name.ToCamelCase(handleAbbreviations: true), value.ToString() ?? "");
            }
        }

        return builder.ToString();
    }
    #region 非泛型版
    /// <summary>
    /// 统一获取内部微服务调用API响应
    /// </summary>
    /// <param name="response">需要检查 IsSuccess 属性是否是 <b>true</b>，否则 response 的属性全为null或默认值，非有效值</param>
    /// <param name="responseType">必须是IServiceResponse类型，且包含无参构造函数</param>
    /// <returns></returns>
    public static async Task<IServiceResponse> GetResponse(this Task<HttpResponseMessage> response, Type responseType)
    {
        var resContent = string.Empty;
        HttpResponseMessage? httpResponse = null;
        Exception? e = null;
        IServiceResponse? res = default;
        try
        {
            httpResponse = await response;
            resContent = await httpResponse.Content.ReadAsStringAsync();
            res = (IServiceResponse?)JsonSerializer.Deserialize(resContent, responseType, JsonShared.GlobalJsonSerializerOptions);
            if (res?.IsServiceNormal() is true)
            {
                return res;
            }
        }
        catch (Exception ex)
        {
            e = ex;
        }

        var errorRes = (IServiceResponse?)Activator.CreateInstance(responseType)!;
        errorRes.Code = ResponseCode.InternalError;
        errorRes.Message = "接口响应出错";

        if (e != null)
        {
            if (e is JsonException jsonEx)
            {
                errorRes.AppendExtraInfo("jsonException", jsonEx.GetJsonErrorDetails(resContent));
            }
            else
            {
                errorRes.AppendExtraInfo("exception", e.ToString());
            }
            var innerException = e.InnerException;
            while (innerException != null)
            {
                errorRes.AppendExtraInfo("exception", innerException.ToString());
                innerException = innerException.InnerException;
            }
        }

        object? contentJson;
        try
        {
            contentJson = res is not null ? null : string.IsNullOrWhiteSpace(resContent) ? "<Empty>" : JsonSerializer.Deserialize<object>(resContent);
        }
        catch
        {
            contentJson = resContent;
        }

        errorRes.AppendExtraInfo("response", new
        {
            Content = res,
            contentJson,
            ResponseType = responseType.GetGenericTypeName(),
            Header = httpResponse?.Headers.ToString(),
            StatusCode = httpResponse?.StatusCode.ToString(),
            httpResponse?.ReasonPhrase,
        });

        if (httpResponse is { IsSuccessStatusCode: false })
        {
            errorRes.AppendExtraInfo("request", await FormatSource(httpResponse));
        }

        GlobalLog.LogError($"{httpResponse?.RequestMessage?.RequestUri}接口响应出错：{errorRes.ToJsonStringForce()}");

        return errorRes;

        static async Task<object> FormatSource(HttpResponseMessage httpResponse)
        {
            var content = httpResponse.RequestMessage?.Content is { } httpContent ? await httpContent.ReadAsStringAsync() : null;

            return new
            {
                RequestMsg = httpResponse.RequestMessage?.ToString(),
                Content = content,
                httpResponse.RequestMessage?.RequestUri
            };
        }
    }


    #endregion
}

