using System.Text.Json;
using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.GlobalJson.Interfaces;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Dapr.Modules;


public static class ModuleDaprProviderClientConnectorBuilderExtensions
{
    public static ModuleDaprProviderClientConnectorGuide UseProviderDapr(
        this ModuleRegisterCentreGuide guide, Action<ModuleDaprProviderClientConnectorOption>? action = null)
    {
        guide.SetCentreServerClientConnector<ServerInvocationDaprHttpProvider>();
        return new ModuleDaprProviderClientConnectorGuide().Register(action);
    }
}

public class ModuleDaprProviderClientConnector(ModuleDaprProviderClientConnectorOption option)
    : MoModuleWithDependencies<ModuleDaprProviderClientConnector, ModuleDaprProviderClientConnectorOption,
        ModuleDaprProviderClientConnectorGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DaprProviderClientConnector;
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleRegisterCentreGuide>().Register();
        DependsOnModule<ModuleGlobalJsonGuide>().Register();
        DependsOnModule<ModuleDaprClientGuide>().Register();
    }
}

public class ModuleDaprProviderClientConnectorGuide : MoModuleGuide<ModuleDaprProviderClientConnector,
    ModuleDaprProviderClientConnectorOption, ModuleDaprProviderClientConnectorGuide>
{


}

public class ModuleDaprProviderClientConnectorOption : MoModuleOption<ModuleDaprProviderClientConnector>
{
}



public class ServerInvocationDaprHttpProvider(DaprClient client, ILogger<ServerInvocationDaprHttpProvider> logger, IGlobalJsonOption jsonOption) : IRegisterCentreServerInvocationConnector
{
    public async Task<Res<TResponse>> GetAsync<TResponse>(string appid, string callbackUrl)
    {
        var content = "";
        try
        {
            var response = await client.InvokeMethodWithResponseAsync(
                client.CreateInvokeMethodRequest(HttpMethod.Get, appid,
                    callbackUrl, []));
            content = await response.Content.ReadAsStringAsync();
            var res = JsonSerializer.Deserialize<TResponse>(content, jsonOption.GlobalOptions);
            if (res == null)
                throw new InvocationException(appid, callbackUrl,
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
            logger.LogError(jsonException, "执行{0}服务{1}失败:{2}，Json数据：{3}", appid, callbackUrl, message, content);
            return Res.Fail(ResponseCode.BadRequest, "执行{0}服务{1}失败:{2}，Json数据：{3}", appid, callbackUrl, message, content);
        }
        catch (Exception e)
        {
            var message = e.GetMessageRecursively();
            logger.LogError(e, "执行{0}服务{1}失败:{2}", appid, callbackUrl, message);
            return Res.Fail(ResponseCode.BadRequest, "执行{0}服务{1}失败:{2}", appid, callbackUrl, message);  
        }
    }

    public async Task<Dictionary<string, Res<TResponse>>> GetAsync<TResponse>(List<string> appid, string callbackUrl)
    {
        var dict = new Dictionary<string, Res<TResponse>>();
        foreach (var t in appid)
        {
            var res = await GetAsync<TResponse>(t, callbackUrl);
            dict.Add(t, res);
        }

        return dict;
    }

    public async Task<Res<TResponse>> PostAsync<TRequest, TResponse>(string appid, string callbackUrl, TRequest req)
    {
        var content = "";
        try
        {
            var response = await client.InvokeMethodWithResponseAsync(
                client.CreateInvokeMethodRequest(HttpMethod.Post, appid,
                    callbackUrl, [], req));

            content = await response.Content.ReadAsStringAsync();
            var res = JsonSerializer.Deserialize<TResponse>(content, jsonOption.GlobalOptions);
            if (res == null)
                throw new InvocationException(appid, callbackUrl,
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
            logger.LogError(jsonException, "执行{0}服务{1}失败:{2}，Json数据：{3}", appid, callbackUrl, message, content);
            return Res.Fail(ResponseCode.BadRequest, "执行{0}服务{1}失败:{2}，Json数据：{3}", appid, callbackUrl, message, content);
        }
        catch (Exception e)
        {
            var message = e.GetMessageRecursively();
            logger.LogError(e, "执行{0}服务{1}失败:{2}", appid, callbackUrl, message);
            return Res.Fail(ResponseCode.BadRequest, "执行{0}服务{1}失败:{2}", appid, callbackUrl, message);
        }
    }

    public async Task<Dictionary<string, Res<TResponse>>> PostAsync<TRequest, TResponse>(List<string> appid, string callbackUrl, TRequest req)
    {
        var dict = new Dictionary<string, Res<TResponse>>();
        foreach (var t in appid)
        {
            var res = await PostAsync<TRequest, TResponse>(t, callbackUrl, req);
            dict.Add(t, res);
        }

        return dict;
    }
}