using Monica.Core.Execution;
using Monica.Repository.UnitOfWork.Abstractions;

namespace Monica.Repository.UnitOfWork.Services.Behaviors;

/// <summary>
/// Executes one business operation inside the ambient Monica unit of work.
/// </summary>
/// <remarks>
/// Nested execution adapters join the current unit of work. The outermost behavior owns the commit; failure handling
/// is delegated to <see cref="IUnitOfWorkManager.RunAsync{T}(Func{Task{T}},Monica.Repository.UnitOfWork.Models.UnitOfWorkScopeOptions?,CancellationToken)"/>.
/// </remarks>
public sealed class UnitOfWorkExecutionBehavior<TInput, TResult>(IUnitOfWorkManager unitOfWorkManager)
    : IExecutionBehavior<TInput, TResult>
{
    /// <inheritdoc />
    public Task<TResult> ExecuteAsync(
        ExecutionContext<TInput> context,
        ExecutionDelegate<TResult> next)
    {
        return unitOfWorkManager.RunAsync(next.Invoke, cancellationToken: context.CancellationToken);
    }
}
