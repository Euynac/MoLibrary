using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Monica.Core.JsonSerialization;
using Monica.Core.Logging;
using Monica.Core.JsonSerialization.Services;
using Monica.Tool.Extensions;
using Monica.Tool.General;
using Monica.Core.Results;

namespace Monica.Framework.Extensions;

public static class HttpApiExtensions
{
    private static readonly ILogger _logger = LogManager.For(typeof(HttpApiExtensions));
    /// <summary>
    /// Unifiedly obtain API responses from internal microservice calls
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="response">Need to check if the IsSuccess property is <b>true</b>,otherwise <typeparamref name="TResponse"/> All attributes are null or default values, not valid values</param>
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
            Status = ResStatus.InternalError,
            Message = "接口响应出错",
        };

        if (e != null)
        {
            if (e is JsonException jsonEx)
            {
                errorRes.AppendMetadata("jsonException", jsonEx.GetJsonErrorDetails(resContent));
            }
            else
            {
                errorRes.AppendMetadata("exception", e.ToString().Split('\n'));
            }
            var innerException = e.InnerException;
            while (innerException != null)
            {
                errorRes.AppendMetadata("exception", innerException.ToString().Split('\n'));
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

        errorRes.AppendMetadata("response", new
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
            errorRes.AppendMetadata("request", await FormatSource(httpResponse));
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
    /// Unifiedly obtain API responses from internal microservice calls
    /// </summary>
    /// <param name="response">Need to check if the IsSuccess property is <b>true</b>, otherwise the attributes of response are all null or default values, which are not valid values.</param>
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
        errorRes.Status = ResStatus.InternalError;
        errorRes.Message = "接口响应出错";

        if (e != null)
        {
            if (e is JsonException jsonEx)
            {
                errorRes.AppendMetadata("jsonException", jsonEx.GetJsonErrorDetails(resContent));
            }
            else
            {
                errorRes.AppendMetadata("exception", e.ToString().Split('\n'));
            }
            var innerException = e.InnerException;
            while (innerException != null)
            {
                errorRes.AppendMetadata("exception", innerException.ToString().Split('\n'));
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

        errorRes.AppendMetadata("response", new
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
            errorRes.AppendMetadata("request", await FormatSource(httpResponse));
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

