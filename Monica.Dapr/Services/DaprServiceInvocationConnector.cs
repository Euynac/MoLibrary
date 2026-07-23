using System.Text.Json;
using Dapr.Client;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.Results;
using Monica.Core.Results.Abstractions;
using Monica.ServiceDiscovery.ServiceInvocation.Abstractions;

namespace Monica.Dapr.Services;

/// <summary>
/// Dapr-based service invocation connector.
/// </summary>
public class DaprServiceInvocationConnector(
    DaprClient client,
    IHttpClientFactory httpClientFactory,
    ILogger<DaprServiceInvocationConnector> logger,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider) : IServiceInvocationConnector
{
    internal const string HttpClientName = "Monica.Dapr.ServiceInvocation";

    public async Task<Res<TResponse>> GetAsync<TResponse>(string appId, string callbackUrl)
    {
        var content = "";
        try
        {
            using var request = client.CreateInvokeMethodRequest(
                HttpMethod.Get,
                appId,
                callbackUrl,
                []);
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request);
            content = await response.Content.ReadAsStringAsync();
            var res = JsonSerializer.Deserialize<TResponse>(content, jsonSerializerOptionsProvider.SerializerOptions);
            if (res == null)
            {
                throw new InvocationException(appId, callbackUrl,
                    new Exception("JSON deserialization returned null."), response);
            }

            if (res is IResultEnvelope serviceResponse)
            {
                serviceResponse.AttachOriginIfMalformed(content);
            }

            return res;
        }
        catch (JsonException jsonException)
        {
            var message = jsonException.GetMessageRecursively();
            logger.LogError(jsonException,
                "Failed to invoke service '{AppId}' at '{CallbackUrl}': {Message}. JSON payload: {Content}",
                appId, callbackUrl, message, content);
            return Res.Fail(ResStatus.BadRequest,
                "Failed to invoke service '{0}' at '{1}': {2}. JSON payload: {3}",
                appId, callbackUrl, message, content);
        }
        catch (Exception exception)
        {
            var message = exception.GetMessageRecursively();
            logger.LogError(exception, "Failed to invoke service '{AppId}' at '{CallbackUrl}': {Message}",
                appId, callbackUrl, message);
            return Res.Fail(ResStatus.BadRequest, "Failed to invoke service '{0}' at '{1}': {2}",
                appId, callbackUrl, message);
        }
    }

    public async Task<Dictionary<string, Res<TResponse>>> GetAsync<TResponse>(List<string> appIds, string callbackUrl)
    {
        var dict = new Dictionary<string, Res<TResponse>>();
        foreach (var appId in appIds)
        {
            var res = await GetAsync<TResponse>(appId, callbackUrl);
            dict.Add(appId, res);
        }

        return dict;
    }

    public async Task<Res<TResponse>> PostAsync<TRequest, TResponse>(string appId, string callbackUrl, TRequest request)
    {
        var content = "";
        try
        {
            using var invocationRequest = client.CreateInvokeMethodRequest(
                HttpMethod.Post,
                appId,
                callbackUrl,
                [],
                request);
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(invocationRequest);

            content = await response.Content.ReadAsStringAsync();
            var res = JsonSerializer.Deserialize<TResponse>(content, jsonSerializerOptionsProvider.SerializerOptions);
            if (res == null)
            {
                throw new InvocationException(appId, callbackUrl,
                    new Exception("JSON deserialization returned null."), response);
            }

            if (res is IResultEnvelope serviceResponse)
            {
                serviceResponse.AttachOriginIfMalformed(content);
            }

            return res;
        }
        catch (JsonException jsonException)
        {
            var message = jsonException.GetMessageRecursively();
            logger.LogError(jsonException,
                "Failed to invoke service '{AppId}' at '{CallbackUrl}': {Message}. JSON payload: {Content}",
                appId, callbackUrl, message, content);
            return Res.Fail(ResStatus.BadRequest,
                "Failed to invoke service '{0}' at '{1}': {2}. JSON payload: {3}",
                appId, callbackUrl, message, content);
        }
        catch (Exception exception)
        {
            var message = exception.GetMessageRecursively();
            logger.LogError(exception, "Failed to invoke service '{AppId}' at '{CallbackUrl}': {Message}",
                appId, callbackUrl, message);
            return Res.Fail(ResStatus.BadRequest, "Failed to invoke service '{0}' at '{1}': {2}",
                appId, callbackUrl, message);
        }
    }

    public async Task<Dictionary<string, Res<TResponse>>> PostAsync<TRequest, TResponse>(List<string> appIds, string callbackUrl, TRequest request)
    {
        var dict = new Dictionary<string, Res<TResponse>>();
        foreach (var appId in appIds)
        {
            var res = await PostAsync<TRequest, TResponse>(appId, callbackUrl, request);
            dict.Add(appId, res);
        }

        return dict;
    }
}
