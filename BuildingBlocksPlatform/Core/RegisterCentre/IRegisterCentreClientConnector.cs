using BuildingBlocksPlatform.Extensions;
using BuildingBlocksPlatform.SeedWork;

namespace BuildingBlocksPlatform.Core.RegisterCentre;

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



public class DaprHttpForConnectClient(DaprClient client, ILogger<DaprHttpForConnectClient> logger) : IRegisterCentreClientConnector
{
    public async Task<Res<TResponse>> GetAsync<TResponse>(string appid, string callbackUrl)
    {
        try
        {
            return await client.InvokeMethodAsync<TResponse>(HttpMethod.Get, appid, callbackUrl);
        }
        catch (Exception e)
        {
            var message = e.GetMessageRecursively();
            return Res.Fail(ResponseCode.BadRequest, "执行{0}服务{1}失败:{2}", appid, callbackUrl,message);
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
        try
        {
            return await client.InvokeMethodAsync<TRequest, TResponse>(HttpMethod.Post, appid, callbackUrl, req);
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