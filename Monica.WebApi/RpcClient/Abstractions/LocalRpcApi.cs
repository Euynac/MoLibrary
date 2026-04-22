using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Mediator;
using Monica.DependencyInjection.Abstractions;

namespace Monica.WebApi.RpcClient.Abstractions;

/// <summary>
/// Base type for generated in-process RPC clients used by modular monolith deployments.
/// </summary>
/// <remarks>
/// Local RPC implementations dispatch requests directly through <see cref="IMediator"/> inside the
/// current process, so no HTTP serialization or remote transport is involved.
/// </remarks>
public abstract class LocalRpcApi(ICachedServiceProvider serviceProvider) : RpcApi(serviceProvider)
{
    /// <summary>
    /// Gets the mediator used to dispatch local RPC requests.
    /// </summary>
    protected IMediator Mediator => CachedServiceProvider.GetRequiredService<IMediator>();
}
