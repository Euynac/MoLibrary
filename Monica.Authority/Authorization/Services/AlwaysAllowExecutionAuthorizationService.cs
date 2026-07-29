using Monica.Authority.Authorization.Abstractions;
using Monica.Authority.Authorization.Models;

namespace Monica.Authority.Authorization.Services;

/// <summary>
/// Authorization service used by hosts that explicitly allow every Monica execution.
/// </summary>
public sealed class AlwaysAllowExecutionAuthorizationService : IExecutionAuthorizationService
{
    /// <inheritdoc />
    public Task CheckAsync(
        ExecutionAuthorizationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
