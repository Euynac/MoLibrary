using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Implements;

/// <summary>
/// Standalone provider for <see cref="IRegisterCentreServerInvocationConnector"/>.
/// Throws <see cref="NotSupportedException"/> for all methods to indicate that
/// service invocation is not supported in standalone mode.
/// </summary>
public class RegisterCentreServerInvocationConnectorStandaloneProvider : IRegisterCentreServerInvocationConnector
{
    private const string ErrorMessage = "Service invocation is not supported in standalone mode. " +
                                       "If you need distributed service invocation, configure a distributed provider " +
                                       "(e.g., Dapr) using UseDistributedProvider<TProvider>() instead of UseInMemoryProvider().";

    public Task<Res<TResponse>> GetAsync<TResponse>(string appid, string callbackUrl)
    {
        throw new NotSupportedException(ErrorMessage);
    }

    public Task<Dictionary<string, Res<TResponse>>> GetAsync<TResponse>(List<string> appid, string callbackUrl)
    {
        throw new NotSupportedException(ErrorMessage);
    }

    public Task<Res<TResponse>> PostAsync<TRequest, TResponse>(string appid, string callbackUrl, TRequest req)
    {
        throw new NotSupportedException(ErrorMessage);
    }

    public Task<Dictionary<string, Res<TResponse>>> PostAsync<TRequest, TResponse>(List<string> appid, string callbackUrl, TRequest req)
    {
        throw new NotSupportedException(ErrorMessage);
    }
}
