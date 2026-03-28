using System.Text.Json;
using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.ServiceDiscovery.ServiceInvocation.Abstractions;
using Monica.Tool.Results;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDaprServiceInvocationBuilderExtensions
{
    /// <summary>
    /// Registers Dapr as the service invocation provider.
    /// </summary>
    public static ModuleDaprServiceInvocationGuide UseDaprInvocationProvider(
        this ModuleServiceInvocationGuide guide, Action<ModuleDaprServiceInvocationOption>? action = null)
    {
        guide.UseDistributedProvider<DaprServiceInvocationConnector>();
        return new ModuleDaprServiceInvocationGuide().Register(action);
    }
}

/// <summary>
/// Dapr-based service invocation module.
/// </summary>
[ModuleKey(EMoModuleKey.DaprProviderClientConnector)]
public class ModuleDaprServiceInvocation(ModuleDaprServiceInvocationOption option)
    : MoModule<ModuleDaprServiceInvocation, ModuleDaprServiceInvocationOption,
        ModuleDaprServiceInvocationGuide>(option)
{

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleJsonSerializationGuide>().Register();
        DependsOnModule<ModuleDaprClientGuide>().Register();
        DependsOnModule<ModuleServiceInvocationGuide>().Register();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IServiceInvocationConnector, DaprServiceInvocationConnector>();
    }
}

public class ModuleDaprServiceInvocationGuide : MoModuleGuide<ModuleDaprServiceInvocation,
    ModuleDaprServiceInvocationOption, ModuleDaprServiceInvocationGuide>
{
}

public class ModuleDaprServiceInvocationOption : MoModuleOption<ModuleDaprServiceInvocation>
{
}

/// <summary>
/// Dapr-based service invocation connector.
/// </summary>
public class DaprServiceInvocationConnector(
    DaprClient client,
    ILogger<DaprServiceInvocationConnector> logger,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider) : IServiceInvocationConnector
{
    public async Task<Res<TResponse>> GetAsync<TResponse>(string appId, string callbackUrl)
    {
        var content = "";
        try
        {
            var response = await client.InvokeMethodWithResponseAsync(
                client.CreateInvokeMethodRequest(HttpMethod.Get, appId,
                    callbackUrl, []));
            content = await response.Content.ReadAsStringAsync();
            var res = JsonSerializer.Deserialize<TResponse>(content, jsonSerializerOptionsProvider.SerializerOptions);
            if (res == null)
                throw new InvocationException(appId, callbackUrl,
                    new Exception("JSON deserialization returned null."), response);

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
        catch (Exception e)
        {
            var message = e.GetMessageRecursively();
            logger.LogError(e, "Failed to invoke service '{AppId}' at '{CallbackUrl}': {Message}",
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
            var response = await client.InvokeMethodWithResponseAsync(
                client.CreateInvokeMethodRequest(HttpMethod.Post, appId,
                    callbackUrl, [], request));

            content = await response.Content.ReadAsStringAsync();
            var res = JsonSerializer.Deserialize<TResponse>(content, jsonSerializerOptionsProvider.SerializerOptions);
            if (res == null)
                throw new InvocationException(appId, callbackUrl,
                    new Exception("JSON deserialization returned null."), response);

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
        catch (Exception e)
        {
            var message = e.GetMessageRecursively();
            logger.LogError(e, "Failed to invoke service '{AppId}' at '{CallbackUrl}': {Message}",
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
