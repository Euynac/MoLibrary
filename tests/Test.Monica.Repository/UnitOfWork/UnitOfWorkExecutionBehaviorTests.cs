using AwesomeAssertions;
using Monica.Core.Execution;
using Monica.Repository.UnitOfWork.Abstractions;
using Monica.Repository.UnitOfWork.Models;
using Monica.Repository.UnitOfWork.Services.Behaviors;
using Xunit;

namespace Test.Monica.Repository.UnitOfWork;

public sealed class UnitOfWorkExecutionBehaviorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenTerminalSucceeds_ShouldCompleteUnitOfWork()
    {
        var unitOfWork = new TrackingUnitOfWork();
        var behavior = new UnitOfWorkExecutionBehavior<string, int>(new TrackingUnitOfWorkManager(unitOfWork));

        var result = await behavior.ExecuteAsync(
            CreateContext(TestContext.Current.CancellationToken),
            () => Task.FromResult(42));

        result.Should().Be(42);
        unitOfWork.CompleteCount.Should().Be(1);
        unitOfWork.RollbackCount.Should().Be(0);
        unitOfWork.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTerminalFails_ShouldRollbackAndRethrow()
    {
        var unitOfWork = new TrackingUnitOfWork();
        var behavior = new UnitOfWorkExecutionBehavior<string, int>(new TrackingUnitOfWorkManager(unitOfWork));
        var expected = new InvalidOperationException("terminal failed");

        var action = () => behavior.ExecuteAsync(CreateContext(), () => Task.FromException<int>(expected));

        (await action.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(expected);
        unitOfWork.CompleteCount.Should().Be(0);
        unitOfWork.RollbackCount.Should().Be(1);
        unitOfWork.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvocationIsCanceled_ShouldRollbackWithoutCanceledToken()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var unitOfWork = new TrackingUnitOfWork();
        var behavior = new UnitOfWorkExecutionBehavior<string, int>(new TrackingUnitOfWorkManager(unitOfWork));
        var context = CreateContext(cancellation.Token);

        var action = () => behavior.ExecuteAsync(
            context,
            () => Task.FromCanceled<int>(cancellation.Token));

        await action.Should().ThrowAsync<OperationCanceledException>();
        unitOfWork.RollbackCancellationToken.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationAndRollbackFail_ShouldPreserveOperationFailureWithRollbackDetails()
    {
        var rollbackFailure = new InvalidOperationException("rollback failed");
        var unitOfWork = new TrackingUnitOfWork
        {
            RollbackException = rollbackFailure
        };
        var behavior = new UnitOfWorkExecutionBehavior<string, int>(new TrackingUnitOfWorkManager(unitOfWork));
        var operationFailure = new InvalidOperationException("operation failed");

        var action = () => behavior.ExecuteAsync(
            CreateContext(),
            () => Task.FromException<int>(operationFailure));

        var assertion = await action.Should().ThrowAsync<InvalidOperationException>();
        assertion.Which.Should().BeSameAs(operationFailure);
        assertion.Which.Data.Values.Cast<object?>().Should().Contain(rollbackFailure);
        unitOfWork.RollbackCount.Should().Be(1);
    }

    private static ExecutionContext<string> CreateContext(CancellationToken cancellationToken = default)
    {
        var descriptor = ExecutionDescriptor.ForMethod<string, int>(
            new ExecutionPoint("test.unit-of-work"),
            typeof(UnitOfWorkExecutionBehaviorTests),
            entryMethod: null,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);
        return new ExecutionContext<string>(descriptor, "input", cancellationToken: cancellationToken);
    }

    private sealed class TrackingUnitOfWorkManager(TrackingUnitOfWork unitOfWork) : IUnitOfWorkManager
    {
        public IUnitOfWork? Current => null;

        public IUnitOfWork BeginScope(UnitOfWorkScopeOptions? options = null)
        {
            return unitOfWork;
        }

        public Task RunAsync(
            Func<Task> work,
            UnitOfWorkScopeOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return RunCoreAsync(
                async () =>
                {
                    await work();
                    return ExecutionUnit.Value;
                },
                cancellationToken);
        }

        public Task<T> RunAsync<T>(
            Func<Task<T>> work,
            UnitOfWorkScopeOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return RunCoreAsync(work, cancellationToken);
        }

        private async Task<T> RunCoreAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken)
        {
            await using var scope = BeginScope();
            try
            {
                var result = await work();
                await scope.CompleteAsync(cancellationToken);
                return result;
            }
            catch (Exception operationException)
            {
                try
                {
                    await scope.RollbackAsync(CancellationToken.None);
                }
                catch (Exception rollbackException)
                {
                    operationException.Data["Monica.Repository.UnitOfWork.RollbackException"] = rollbackException;
                }

                throw;
            }
        }
    }

    private sealed class TrackingUnitOfWork : IUnitOfWork
    {
        public Guid Id { get; } = Guid.NewGuid();

        public bool IsCompleted => CompleteCount > 0;

        public int CompleteCount { get; private set; }

        public int RollbackCount { get; private set; }

        public int DisposeCount { get; private set; }

        public CancellationToken RollbackCancellationToken { get; private set; }

        public Exception? RollbackException { get; init; }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            CompleteCount++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            RollbackCancellationToken = cancellationToken;
            return RollbackException is null
                ? Task.CompletedTask
                : Task.FromException(RollbackException);
        }

        public void OnCompleted(Func<Task> handler)
        {
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
