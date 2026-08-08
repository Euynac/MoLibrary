using AwesomeAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.UI.UISystemInfo.Services;
using Xunit;

namespace Test.Monica.UI.UISystemInfo.Services;

public sealed class SystemInfoRestartCoordinatorTests
{
    [Fact]
    public async Task Request_WhenDelayElapses_ShouldStopHostOnceAndRejectDuplicates()
    {
        using var lifetime = new TestHostApplicationLifetime();
        var coordinator = CreateCoordinator(lifetime, TimeSpan.Zero);

        var first = coordinator.Request();
        var duplicate = coordinator.Request();
        await coordinator.ScheduledShutdown!.WaitAsync(TestContext.Current.CancellationToken);

        first.Status.Should().Be(SystemInfoRestartStatus.Scheduled);
        duplicate.Status.Should().Be(SystemInfoRestartStatus.AlreadyPending);
        lifetime.StopRequestCount.Should().Be(1);
    }

    [Fact]
    public async Task Request_WhenHostStopsDuringDelay_ShouldCancelPendingWork()
    {
        using var lifetime = new TestHostApplicationLifetime();
        var coordinator = CreateCoordinator(lifetime, TimeSpan.FromHours(1));

        coordinator.Request().Status.Should().Be(SystemInfoRestartStatus.Scheduled);
        lifetime.BeginExternalStop();
        await coordinator.ScheduledShutdown!.WaitAsync(TestContext.Current.CancellationToken);

        lifetime.StopRequestCount.Should().Be(0);
    }

    private static SystemInfoRestartCoordinator CreateCoordinator(
        IHostApplicationLifetime lifetime,
        TimeSpan delay) => new(
        lifetime,
        Options.Create(new ModuleSystemInfoUIOption
        {
            EnableSelfRestartAction = true,
            SelfRestartDelay = delay
        }),
        NullLogger<SystemInfoRestartCoordinator>.Instance);

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => _stopped.Token;

        internal int StopRequestCount { get; private set; }

        public void StopApplication()
        {
            StopRequestCount++;
            _stopping.Cancel();
        }

        internal void BeginExternalStop() => _stopping.Cancel();

        public void Dispose()
        {
            _started.Dispose();
            _stopping.Dispose();
            _stopped.Dispose();
        }
    }
}
