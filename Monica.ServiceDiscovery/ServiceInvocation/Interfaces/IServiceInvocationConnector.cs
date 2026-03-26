using Monica.Tool.MoResponse;

namespace Monica.RegisterCentre.ServiceInvocation.Interfaces;

/// <summary>
/// 服务调用连接器接口，用于服务间 HTTP 调用
/// </summary>
public interface IServiceInvocationConnector
{
    /// <summary>
    /// GET 方法执行调用
    /// </summary>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="appId">目标服务的 AppId</param>
    /// <param name="callbackUrl">回调 URL 路径</param>
    /// <returns>调用结果</returns>
    Task<Res<TResponse>> GetAsync<TResponse>(string appId, string callbackUrl);

    /// <summary>
    /// GET 方法批量执行调用
    /// </summary>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="appIds">目标服务的 AppId 列表</param>
    /// <param name="callbackUrl">回调 URL 路径</param>
    /// <returns>调用结果字典，Key 为 AppId</returns>
    Task<Dictionary<string, Res<TResponse>>> GetAsync<TResponse>(List<string> appIds, string callbackUrl);

    /// <summary>
    /// POST 方法执行调用
    /// </summary>
    /// <typeparam name="TRequest">请求类型</typeparam>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="appId">目标服务的 AppId</param>
    /// <param name="callbackUrl">回调 URL 路径</param>
    /// <param name="request">请求数据</param>
    /// <returns>调用结果</returns>
    Task<Res<TResponse>> PostAsync<TRequest, TResponse>(string appId, string callbackUrl, TRequest request);

    /// <summary>
    /// POST 方法批量执行调用
    /// </summary>
    /// <typeparam name="TRequest">请求类型</typeparam>
    /// <typeparam name="TResponse">响应类型</typeparam>
    /// <param name="appIds">目标服务的 AppId 列表</param>
    /// <param name="callbackUrl">回调 URL 路径</param>
    /// <param name="request">请求数据</param>
    /// <returns>调用结果字典，Key 为 AppId</returns>
    Task<Dictionary<string, Res<TResponse>>> PostAsync<TRequest, TResponse>(List<string> appIds, string callbackUrl, TRequest request);
}
