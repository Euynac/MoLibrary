using Monica.Core.Execution;

namespace Monica.Core.HostedService.Abstractions;

/// <summary>
/// Defines finite scoped work resolved and dispatched by a Monica background service.
/// </summary>
public interface IHostedServiceWorkItem : IExecutionAdapterOwnedComponent
{
    /// <summary>
    /// Executes one bounded work item.
    /// </summary>
    /// <param name="cancellationToken">Signals that the owning service is stopping.</param>
    Task ExecuteAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Defines finite scoped work with a typed input and result.
/// </summary>
/// <typeparam name="TInput">The work-item input type.</typeparam>
/// <typeparam name="TResult">The work-item result type.</typeparam>
public interface IHostedServiceWorkItem<TInput, TResult> : IExecutionAdapterOwnedComponent
{
    /// <summary>
    /// Executes one bounded work item.
    /// </summary>
    /// <param name="input">The work-item input.</param>
    /// <param name="cancellationToken">Signals that the owning service is stopping.</param>
    Task<TResult> ExecuteAsync(TInput input, CancellationToken cancellationToken);
}
