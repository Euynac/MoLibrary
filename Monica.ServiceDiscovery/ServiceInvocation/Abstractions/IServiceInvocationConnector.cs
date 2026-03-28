using Monica.Core.Results;

namespace Monica.ServiceDiscovery.ServiceInvocation.Abstractions;

/// <summary>
/// Service invocation connector interface for inter-service HTTP calls.
/// </summary>
public interface IServiceInvocationConnector
{
    /// <summary>
    /// Executes a GET request.
    /// </summary>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="appId">AppId of the target service.</param>
    /// <param name="callbackUrl">Callback URL path.</param>
    /// <returns>Invocation result.</returns>
    Task<Res<TResponse>> GetAsync<TResponse>(string appId, string callbackUrl);

    /// <summary>
    /// Executes GET requests in batch.
    /// </summary>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="appIds">List of target service AppIds.</param>
    /// <param name="callbackUrl">Callback URL path.</param>
    /// <returns>A dictionary of invocation results keyed by AppId.</returns>
    Task<Dictionary<string, Res<TResponse>>> GetAsync<TResponse>(List<string> appIds, string callbackUrl);

    /// <summary>
    /// Executes a POST request.
    /// </summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="appId">AppId of the target service.</param>
    /// <param name="callbackUrl">Callback URL path.</param>
    /// <param name="request">Request payload.</param>
    /// <returns>Invocation result.</returns>
    Task<Res<TResponse>> PostAsync<TRequest, TResponse>(string appId, string callbackUrl, TRequest request);

    /// <summary>
    /// Executes POST requests in batch.
    /// </summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="appIds">List of target service AppIds.</param>
    /// <param name="callbackUrl">Callback URL path.</param>
    /// <param name="request">Request payload.</param>
    /// <returns>A dictionary of invocation results keyed by AppId.</returns>
    Task<Dictionary<string, Res<TResponse>>> PostAsync<TRequest, TResponse>(List<string> appIds, string callbackUrl, TRequest request);
}
