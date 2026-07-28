using Monica.Core.Execution;
using Monica.Repository.UnitOfWork.Abstractions;

namespace Monica.Repository.UnitOfWork.Services.Behaviors;

/// <summary>
/// Executes one business operation inside the ambient Monica unit of work.
/// </summary>
/// <remarks>
/// Nested execution adapters join the current unit of work. The outermost behavior owns the commit; any exception
/// rolls back the shared transaction before it is rethrown. If rollback also fails, the original operation exception
/// remains primary and the rollback exception is retained in its <see cref="Exception.Data"/> dictionary.
/// </remarks>
public sealed class UnitOfWorkExecutionBehavior<TInput, TResult>(IUnitOfWorkManager unitOfWorkManager)
    : IExecutionBehavior<TInput, TResult>
{
    private const string ROLLBACK_EXCEPTION_DATA_KEY = "Monica.Repository.UnitOfWork.RollbackException";

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync(
        ExecutionContext<TInput> context,
        ExecutionDelegate<TResult> next)
    {
        await using var unitOfWork = unitOfWorkManager.BeginScope();
        try
        {
            var result = await next();
            await unitOfWork.CompleteAsync(context.CancellationToken);
            return result;
        }
        catch (Exception operationException)
        {
            // Rollback is cleanup and must still run when the operation's cancellation token caused the failure.
            try
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                // Preserve the business failure as the primary exception while keeping cleanup diagnostics available.
                operationException.Data[ROLLBACK_EXCEPTION_DATA_KEY] = rollbackException;
            }

            throw;
        }
    }
}
