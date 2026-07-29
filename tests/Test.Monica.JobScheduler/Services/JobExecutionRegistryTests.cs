using AwesomeAssertions;
using Monica.JobScheduler.Services.Support;
using Xunit;

namespace Test.Monica.JobScheduler.Services;

public sealed class JobExecutionRegistryTests
{
    [Fact]
    public async Task DrainAsync_ShouldCloseAdmissionAndWaitForEveryAcceptedExecution()
    {
        var registry = new JobExecutionRegistry();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        registry.OpenAdmission();

        var accepted = registry.TryStart(
            "accepted",
            async () =>
            {
                started.SetResult();
                await release.Task;
            },
            out var execution);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);

        var drain = registry.DrainAsync(TestContext.Current.CancellationToken);
        var rejected = registry.TryStart("late", () => Task.CompletedTask, out _);

        accepted.Should().BeTrue();
        rejected.Should().BeFalse();
        drain.IsCompleted.Should().BeFalse();
        registry.Count.Should().Be(1);

        release.SetResult();
        await drain;
        await execution;
        registry.Count.Should().Be(0);
    }

    [Fact]
    public async Task TryStart_ShouldCoalesceDuplicateExecutionIds()
    {
        var registry = new JobExecutionRegistry();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        registry.OpenAdmission();

        registry.TryStart("same", () => release.Task, out var first).Should().BeTrue();
        registry.TryStart("same", () => Task.CompletedTask, out _).Should().BeFalse();

        release.SetResult();
        await first;
        registry.Count.Should().Be(0);
    }
}
