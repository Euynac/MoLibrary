using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Interfaces;

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