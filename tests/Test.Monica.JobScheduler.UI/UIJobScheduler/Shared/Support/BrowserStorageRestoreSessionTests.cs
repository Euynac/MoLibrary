using AwesomeAssertions;
using Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;
using Xunit;

namespace Test.Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;

public sealed class BrowserStorageRestoreSessionTests
{
    [Fact]
    public async Task DisposeAsync_WhenRestoreCompletesLate_ShouldCancelPublicationAndDrainRestore()
    {
        var restoreStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRestore = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var statePublished = false;
        var session = new BrowserStorageRestoreSession();

        var restoreTask = session.RunAsync(async cancellationToken =>
        {
            restoreStarted.SetResult();
            await releaseRestore.Task;
            if (!cancellationToken.IsCancellationRequested)
            {
                statePublished = true;
            }
        });

        await restoreStarted.Task;
        var disposal = session.DisposeAsync().AsTask();

        disposal.IsCompleted.Should().BeFalse();
        releaseRestore.SetResult();

        await disposal;
        await restoreTask;
        statePublished.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_WhenRepeated_ShouldReturnTheSameCompletedDisposal()
    {
        var session = new BrowserStorageRestoreSession();

        await session.DisposeAsync();
        await session.DisposeAsync();

        var restoreStarted = false;
        await session.RunAsync(_ =>
        {
            restoreStarted = true;
            return Task.CompletedTask;
        });

        restoreStarted.Should().BeFalse();
    }
}
