using Monica.Authority.Authorization.Abstractions;
using Monica.Authority.Authorization.Models;
using Monica.Authority.Identity.Abstractions;
using Monica.Core.Execution;

namespace Monica.Authority.Authorization.Services.Behaviors;

/// <summary>
/// Enforces authorization metadata before a business execution enters its terminal operation.
/// </summary>
public sealed class ExecutionAuthorizationBehavior<TInput, TResult>(
    IExecutionAuthorizationService authorizationService,
    ICurrentPrincipalAccessor principalAccessor)
    : IExecutionBehavior<TInput, TResult>
{
    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync(
        ExecutionContext<TInput> context,
        ExecutionDelegate<TResult> next)
    {
        await authorizationService.CheckAsync(
            new ExecutionAuthorizationContext(
                context.Descriptor,
                principalAccessor.Principal,
                context.Input,
                context.Target,
                context.Features),
            context.CancellationToken);
        return await next();
    }
}
