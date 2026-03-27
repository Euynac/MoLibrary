using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Monica.Core.JsonSerialization;
using Monica.Core.Logging;
using Monica.Core.JsonSerialization.Services;
using Monica.Tool.Extensions;
using Monica.Tool.General;
using Monica.Tool.Results;

namespace Monica.Framework.Extensions;

public static class HttpApiExtensions
{
    private static readonly ILogger _logger = LogManager.For(typeof(HttpApiExtensions));
    /// <summary>
    /// 统一获取内部微服务调用API响应
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="response">需要检查 IsSuccess 属性是否是 <b>true</b>，否则 <typeparamref name="TResponse"/> 的属性全为null或默认值，非有效值</param>
    /// <returns></returns>
    public static async Task<TResponse> GetResponse<TResponse>(this Task<HttpResponseMessage> response)
        where TResponse : class, IResultEnvelope, new()
    {
        var resContent = string.Empty;
        HttpResponseMessage? httpResponse = null;
        Exception? e = null;
        TResponse? res = default;
        try
        {
            httpResponse = await response;
            resContent = await httpResponse.Content.ReadAsStringAsync();
            res = JsonSerializer.Deserialize<TResponse>(resContent, JsonSerializerOptionsProvider.SharedSerializerOptions);
            res?.AttachOriginIfMalformed(resContent);
            if (res?.IsRemoteResultHealthy() is true)
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
            Code = ResStatus.InternalError,
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
                errorRes.AppendExtraInfo("exception", e.ToString().Split('\n'));
            }
            var innerException = e.InnerException;
            while (innerException != null)
            {
                errorRes.AppendExtraInfo("exception", innerException.ToString().Split('\n'));
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
            ResponseType = typeof(TResponse).GetCleanFullName(),
            Header = httpResponse?.Headers.ToString(),
            StatusCode = httpResponse?.StatusCode.ToString(),
            httpResponse?.ReasonPhrase,
        });


        if (httpResponse is { IsSuccessStatusCode: false })
        {
            errorRes.AppendExtraInfo("request", await FormatSource(httpResponse));
        }

        _logger.LogError($"{httpResponse?.RequestMessage?.RequestUri}接口响应出错：{errorRes.ToJsonStringForce()}");

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
                builder.Add(property.Name.ToCamelCase(), value.ToString() ?? "");
            }
        }

        return builder.ToString();
    }
    #region 非泛型版
    /// <summary>
    /// 统一获取内部微服务调用API响应
    /// </summary>
    /// <param name="response">需要检查 IsSuccess 属性是否是 <b>true</b>，否则 response 的属性全为null或默认值，非有效值</param>
    /// <param name="responseType">Must implement <see cref="IResultEnvelope" /> and expose a parameterless constructor.</param>
    /// <returns></returns>
    public static async Task<IResultEnvelope> GetResponse(this Task<HttpResponseMessage> response, Type responseType)
    {
        var resContent = string.Empty;
        HttpResponseMessage? httpResponse = null;
        Exception? e = null;
        IResultEnvelope? res = default;
        try
        {
            httpResponse = await response;
            resContent = await httpResponse.Content.ReadAsStringAsync();
            res = (IResultEnvelope?)JsonSerializer.Deserialize(resContent, responseType, JsonSerializerOptionsProvider.SharedSerializerOptions);
            if (res?.IsRemoteResultHealthy() is true)
            {
                return res;
            }
        }
        catch (Exception ex)
        {
            e = ex;
        }

        var errorRes = (IResultEnvelope?)Activator.CreateInstance(responseType)!;
        errorRes.Code = ResStatus.InternalError;
        errorRes.Message = "接口响应出错";

        if (e != null)
        {
            if (e is JsonException jsonEx)
            {
                errorRes.AppendExtraInfo("jsonException", jsonEx.GetJsonErrorDetails(resContent));
            }
            else
            {
                errorRes.AppendExtraInfo("exception", e.ToString().Split('\n'));
            }
            var innerException = e.InnerException;
            while (innerException != null)
            {
                errorRes.AppendExtraInfo("exception", innerException.ToString().Split('\n'));
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
            ResponseType = responseType.GetCleanFullName(),
            Header = httpResponse?.Headers.ToString(),
            StatusCode = httpResponse?.StatusCode.ToString(),
            httpResponse?.ReasonPhrase,
        });

        if (httpResponse is { IsSuccessStatusCode: false })
        {
            errorRes.AppendExtraInfo("request", await FormatSource(httpResponse));
        }

        _logger.LogError($"{httpResponse?.RequestMessage?.RequestUri}接口响应出错：{errorRes.ToJsonStringForce()}");

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

