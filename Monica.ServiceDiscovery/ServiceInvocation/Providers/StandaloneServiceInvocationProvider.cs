using Monica.ServiceDiscovery.ServiceInvocation.Abstractions;
using Monica.Tool.MoResponse;

namespace Monica.ServiceDiscovery.ServiceInvocation.Providers;

/// <summary>
/// 独立模式服务调用提供者（不支持服务间调用）
/// </summary>
public class StandaloneServiceInvocationProvider : IServiceInvocationConnector
{
    private const string ErrorMessage = "服务调用在独立模式下不可用。请使用 UseDistributedProvider<TProvider>() 配置分布式调用提供者。";

    public Task<Res<TResponse>> GetAsync<TResponse>(string appId, string callbackUrl)
    {
        throw new NotSupportedException(ErrorMessage);
    }

    public Task<Dictionary<string, Res<TResponse>>> GetAsync<TResponse>(List<string> appIds, string callbackUrl)
    {
        throw new NotSupportedException(ErrorMessage);
    }

    public Task<Res<TResponse>> PostAsync<TRequest, TResponse>(string appId, string callbackUrl, TRequest request)
    {
        throw new NotSupportedException(ErrorMessage);
    }

    public Task<Dictionary<string, Res<TResponse>>> PostAsync<TRequest, TResponse>(List<string> appIds, string callbackUrl, TRequest request)
    {
        throw new NotSupportedException(ErrorMessage);
    }
}
