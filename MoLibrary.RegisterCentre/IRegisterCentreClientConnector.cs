using System.Text.Json;
using Dapr.Client;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.GlobalJson.Interfaces;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre;

public interface IRegisterCentreClientConnector
{
    /// <summary>
    /// Get 方法执行调用
    /// </summary>
    /// <param name="appid"></param>
    /// <param name="callbackUrl"></param>
    /// <returns></returns>
    Task<Res<TResponse>> GetAsync<TResponse>(string appid, string callbackUrl);

    /// <summary>
    /// Get 方法批量执行调用
    /// </summary>
    /// <returns></returns>
    Task<Dictionary<string, Res<TResponse>>> GetAsync<TResponse>(List<string> appid, string callbackUrl);

    /// <summary>
    /// POST 方法执行调用
    /// </summary>
    /// <returns></returns>
    Task<Res<TResponse>> PostAsync<TRequest, TResponse>(string appid, string callbackUrl, TRequest req);
    /// <summary>
    /// POST 方法批量执行调用
    /// </summary>
    /// <returns></returns>
    Task<Dictionary<string, Res<TResponse>>> PostAsync<TRequest, TResponse>(List<string> appid, string callbackUrl, TRequest req);
}



public class DaprHttpForConnectClient(DaprClient client, ILogger<DaprHttpForConnectClient> logger, IGlobalJsonOption jsonOption) : IRegisterCentreClientConnector
{
    public async Task<Res<TResponse>> GetAsync<TResponse>(string appid, string callbackUrl)
    {
        var response = await client.InvokeMethodWithResponseAsync(
            client.CreateInvokeMethodRequest(HttpMethod.Get, appid,
                callbackUrl, []));

        try
        {
            var content = await response.Content.ReadAsStringAsync();
            var res = JsonSerializer.Deserialize<TResponse>(content, jsonOption.GlobalOptions);
            if (res == null)
                throw new InvocationException(appid, callbackUrl,
                    new Exception("Json序列化为空"), response);

            if (res is IServiceResponse serviceResponse)
            {
                serviceResponse.AutoParseResponseFromOrigin(content);
            }

            //var res = await response.Content.ReadFromJsonAsync<TResponse>(jsonOption.GlobalOptions);
            

            return res;
        }
        catch (Exception e)
        {
            var message = e.GetMessageRecursively();
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

        var response = await client.InvokeMethodWithResponseAsync(
            client.CreateInvokeMethodRequest(HttpMethod.Post, appid,
                callbackUrl, [], req));

        try
        {
            var content = await response.Content.ReadAsStringAsync();
            var res = JsonSerializer.Deserialize<TResponse>(content, jsonOption.GlobalOptions);
            if (res == null)
                throw new InvocationException(appid, callbackUrl,
                    new Exception("Json序列化为空"), response);

            if (res is IServiceResponse serviceResponse)
            {
                serviceResponse.AutoParseResponseFromOrigin(content);
            }
            return res;
        }
        catch (Exception e)
        {
            var message = e.GetMessageRecursively();
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