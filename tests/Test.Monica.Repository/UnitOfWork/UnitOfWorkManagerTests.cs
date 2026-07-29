using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Monica.Repository.UnitOfWork.Abstractions;
using Monica.Repository.UnitOfWork.Services;
using Xunit;

namespace Test.Monica.Repository.UnitOfWork;

public sealed class UnitOfWorkManagerTests
{
    private const string ROLLBACK_EXCEPTION_DATA_KEY = "Monica.Repository.UnitOfWork.RollbackException";

    [Fact]
    public async Task RunAsync_WhenOperationAndRollbackFail_ShouldPreserveOriginalFailure()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var rollbackFailure = new InvalidOperationException("rollback failed");
        var ambientUnitOfWork = new FailingRollbackUnitOfWork(rollbackFailure);
        var manager = new UnitOfWorkManager(new UnusedServiceScopeFactory());
        manager.SetUnitOfWork(ambientUnitOfWork);
        var operationFailure = new TestOperationException("operation failed");

        var action = () => manager.RunAsync(
            () => ThrowOperationAsync(operationFailure),
            cancellationToken: cancellation.Token);

        var thrown = (await action.Should().ThrowAsync<TestOperationException>()).Which;

        thrown.Should().BeSameAs(operationFailure);
        thrown.StackTrace.Should().Contain(nameof(ThrowOperationAsync));
        thrown.Data[ROLLBACK_EXCEPTION_DATA_KEY].Should().BeSameAs(rollbackFailure);
        ambientUnitOfWork.RollbackCancellationToken.CanBeCanceled.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WhenNonGenericOperationFails_ShouldPreserveOriginalFailure()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var ambientUnitOfWork = new FailingRollbackUnitOfWork(rollbackFailure: null);
        var manager = new UnitOfWorkManager(new UnusedServiceScopeFactory());
        manager.SetUnitOfWork(ambientUnitOfWork);
        var operationFailure = new TestOperationException("operation failed");

        var action = () => manager.RunAsync(
            () => ThrowOperationWithoutResultAsync(operationFailure),
            cancellationToken: cancellation.Token);

        var thrown = (await action.Should().ThrowAsync<TestOperationException>()).Which;

        thrown.Should().BeSameAs(operationFailure);
        thrown.StackTrace.Should().Contain(nameof(ThrowOperationWithoutResultAsync));
        thrown.Data.Contains(ROLLBACK_EXCEPTION_DATA_KEY).Should().BeFalse();
        ambientUnitOfWork.RollbackCancellationToken.CanBeCanceled.Should().BeFalse();
    }

    private static async Task<int> ThrowOperationAsync(TestOperationException exception)
    {
        await Task.Yield();
        throw exception;
    }

    private static async Task ThrowOperationWithoutResultAsync(TestOperationException exception)
    {
        await Task.Yield();
        throw exception;
    }

    private sealed class TestOperationException(string message) : Exception(message);

    private sealed class FailingRollbackUnitOfWork(Exception? rollbackFailure) : IUnitOfWork
    {
        public Guid Id { get; } = Guid.NewGuid();

        public bool IsCompleted => false;

        public CancellationToken RollbackCancellationToken { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCancellationToken = cancellationToken;
            return rollbackFailure is null
                ? Task.CompletedTask
                : Task.FromException(rollbackFailure);
        }

        public void OnCompleted(Func<Task> handler)
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private sealed class UnusedServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            throw new InvalidOperationException("The test uses an ambient unit of work and must not create a scope.");
        }
    }
}
