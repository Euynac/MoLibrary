using System.Text.Json;
using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.GlobalJson.Interfaces;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.RegisterCentre.ServiceInvocation.Interfaces;
using MoLibrary.RegisterCentre.ServiceInvocation.Modules;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Dapr.Modules;

public static class ModuleDaprServiceInvocationBuilderExtensions
{
    /// <summary>
    /// 使用 Dapr 作为服务调用提供者
    /// </summary>
    public static ModuleDaprServiceInvocationGuide UseDaprInvocationProvider(
        this ModuleServiceInvocationGuide guide, Action<ModuleDaprServiceInvocationOption>? action = null)
    {
        guide.UseDistributedProvider<DaprServiceInvocationConnector>();
        return new ModuleDaprServiceInvocationGuide().Register(action);
    }
}

/// <summary>
/// Dapr 服务调用模块
/// </summary>
public class ModuleDaprServiceInvocation(ModuleDaprServiceInvocationOption option)
    : MoModuleWithDependencies<ModuleDaprServiceInvocation, ModuleDaprServiceInvocationOption,
        ModuleDaprServiceInvocationGuide>(option)
{
    public override EMoModules CurModuleEnum() => EMoModules.DaprProviderClientConnector;

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleGlobalJsonGuide>().Register();
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
/// 基于 Dapr 的服务调用连接器实现
/// </summary>
public class DaprServiceInvocationConnector(
    DaprClient client,
    ILogger<DaprServiceInvocationConnector> logger,
    IGlobalJsonOption jsonOption) : IServiceInvocationConnector
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
            var res = JsonSerializer.Deserialize<TResponse>(content, jsonOption.GlobalOptions);
            if (res == null)
                throw new InvocationException(appId, callbackUrl,
                    new Exception("Json序列化为空"), response);

            if (res is IMoResponse serviceResponse)
            {
                serviceResponse.AutoParseResponseFromOrigin(content);
            }

            return res;
        }
        catch (JsonException jsonException)
        {
            var message = jsonException.GetMessageRecursively();
            logger.LogError(jsonException, "执行{0}服务{1}失败:{2}，Json数据：{3}", appId, callbackUrl, message, content);
            return Res.Fail(ResponseCode.BadRequest, "执行{0}服务{1}失败:{2}，Json数据：{3}", appId, callbackUrl, message, content);
        }
        catch (Exception e)
        {
            var message = e.GetMessageRecursively();
            logger.LogError(e, "执行{0}服务{1}失败:{2}", appId, callbackUrl, message);
            return Res.Fail(ResponseCode.BadRequest, "执行{0}服务{1}失败:{2}", appId, callbackUrl, message);
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
            var res = JsonSerializer.Deserialize<TResponse>(content, jsonOption.GlobalOptions);
            if (res == null)
                throw new InvocationException(appId, callbackUrl,
                    new Exception("Json序列化为空"), response);

            if (res is IMoResponse serviceResponse)
            {
                serviceResponse.AutoParseResponseFromOrigin(content);
            }
            return res;
        }
        catch (JsonException jsonException)
        {
            var message = jsonException.GetMessageRecursively();
            logger.LogError(jsonException, "执行{0}服务{1}失败:{2}，Json数据：{3}", appId, callbackUrl, message, content);
            return Res.Fail(ResponseCode.BadRequest, "执行{0}服务{1}失败:{2}，Json数据：{3}", appId, callbackUrl, message, content);
        }
        catch (Exception e)
        {
            var message = e.GetMessageRecursively();
            logger.LogError(e, "执行{0}服务{1}失败:{2}", appId, callbackUrl, message);
            return Res.Fail(ResponseCode.BadRequest, "执行{0}服务{1}失败:{2}", appId, callbackUrl, message);
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
