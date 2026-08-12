using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Diagnostics.Facades;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Services.Support;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class MonicaStartupTimingTests
{
    private static readonly TimeSpan HANG_GUARD = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AddMonica_WithoutStartupMarker_ShouldKeepTimingAbsentAndFreezeTerminalDiagnostics()
    {
        var builder = Host.CreateApplicationBuilder();
        ConfigureMonica(builder);
        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var diagnostics = host.Services.GetRequiredService<ModuleDiagnosticsFacade>();

        var beforeStart = diagnostics.GetSnapshot().Data!;

        application.StartupTiming.Should().BeNull();
        beforeStart.IsFinal.Should().BeTrue();
        beforeStart.Summary.ApplicationStartupDurationMs.Should().BeNull();
        diagnostics.GetSnapshot().Data.Should().BeSameAs(beforeStart);

        await host.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            application.StartupTiming.Should().BeNull();
            diagnostics.GetSnapshot().Data.Should().BeSameAs(beforeStart);
        }
        finally
        {
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task StartupTiming_WhenHostStarts_ShouldIncludePreCompositionWorkAndFreezeAtApplicationStarted()
    {
        var startup = MonicaStartup.Start();
        SpinWait.SpinUntil(
                () => Stopwatch.GetElapsedTime(startup.StartedTimestamp) >= TimeSpan.FromMilliseconds(5),
                HANG_GUARD)
            .Should().BeTrue();
        var elapsedBeforeComposition = Stopwatch.GetElapsedTime(startup.StartedTimestamp);
        var builder = Host.CreateApplicationBuilder();
        ConfigureMonica(builder, startup);
        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var diagnostics = host.Services.GetRequiredService<ModuleDiagnosticsFacade>();
        var pending = application.StartupTiming;
        var beforeReady = diagnostics.GetSnapshot().Data!;
        var readinessSignal = application.Profiling.WaitForApplicationReadyAsync(
            TestContext.Current.CancellationToken);

        pending.Should().NotBeNull();
        pending!.StartedAtUtc.Should().Be(startup.StartedAtUtc);
        pending.IsReady.Should().BeFalse();
        pending.ReadyAtUtc.Should().BeNull();
        pending.DurationMs.Should().BeNull();
        beforeReady.IsFinal.Should().BeTrue();
        beforeReady.Summary.ApplicationStartupDurationMs.Should().BeNull();
        diagnostics.GetSnapshot().Data.Should().BeSameAs(beforeReady);
        readinessSignal.IsCompleted.Should().BeFalse();

        await host.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await readinessSignal.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            var ready = application.StartupTiming;
            ready.Should().NotBeNull();
            ready!.IsReady.Should().BeTrue();
            ready.ReadyAtUtc.Should().BeOnOrAfter(ready.StartedAtUtc);
            ready.DurationMs.Should().BeGreaterThanOrEqualTo(elapsedBeforeComposition.TotalMilliseconds);

            var snapshot = diagnostics.GetSnapshot().Data!;
            snapshot.IsFinal.Should().BeTrue();
            snapshot.Should().NotBeSameAs(beforeReady);
            snapshot.Revision.Should().BeGreaterThan(beforeReady.Revision);
            snapshot.SchemaVersion.Should().Be(ModuleDiagnosticsSnapshot.CURRENT_SCHEMA_VERSION);
            snapshot.Summary.ApplicationStartupDurationMs.Should().Be(ready.DurationMs);
            diagnostics.CreateExport().Data!.Summary.ApplicationStartupDurationMs.Should().Be(ready.DurationMs);

            application.Profiling.MarkApplicationReady();
            application.StartupTiming.Should().BeSameAs(ready);
            diagnostics.GetSnapshot().Data.Should().BeSameAs(snapshot);
        }
        finally
        {
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void AddMonica_WhenStartupMarkerIsReused_ShouldRejectTheSecondHost()
    {
        var startup = MonicaStartup.Start();
        var firstBuilder = Host.CreateApplicationBuilder();
        ConfigureMonica(firstBuilder, startup);
        var secondBuilder = Host.CreateApplicationBuilder();

        Action reuse = () => ConfigureMonica(secondBuilder, startup);

        reuse.Should().Throw<InvalidOperationException>()
            .WithMessage("*startup marker has already been assigned*");
    }

    [Fact]
    public async Task StartupTiming_WhenTwoHostsStartConcurrently_ShouldRemainHostOwned()
    {
        var firstStartup = MonicaStartup.Start();
        var secondStartup = MonicaStartup.Start();
        var firstBuilder = Host.CreateApplicationBuilder();
        var secondBuilder = Host.CreateApplicationBuilder();
        ConfigureMonica(firstBuilder, firstStartup);
        ConfigureMonica(secondBuilder, secondStartup);
        using var firstHost = firstBuilder.Build();
        using var secondHost = secondBuilder.Build();
        var firstApplication = firstHost.Services.GetRequiredService<MonicaApplication>();
        var secondApplication = secondHost.Services.GetRequiredService<MonicaApplication>();

        await Task.WhenAll(
            firstHost.StartAsync(TestContext.Current.CancellationToken),
            secondHost.StartAsync(TestContext.Current.CancellationToken));
        try
        {
            firstApplication.StartupTiming.Should().NotBeNull();
            secondApplication.StartupTiming.Should().NotBeNull();
            firstApplication.StartupTiming!.StartedAtUtc.Should().Be(firstStartup.StartedAtUtc);
            secondApplication.StartupTiming!.StartedAtUtc.Should().Be(secondStartup.StartedAtUtc);
            firstApplication.StartupTiming.Should().NotBeSameAs(secondApplication.StartupTiming);
            firstApplication.StartupTiming.IsReady.Should().BeTrue();
            secondApplication.StartupTiming.IsReady.Should().BeTrue();
        }
        finally
        {
            await Task.WhenAll(
                firstHost.StopAsync(TestContext.Current.CancellationToken),
                secondHost.StopAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task StartupTiming_WhileALaterHostedServiceIsStarting_ShouldRemainPending()
    {
        var startup = MonicaStartup.Start();
        var gate = new StartupGate();
        var builder = Host.CreateApplicationBuilder();
        ConfigureMonica(builder, startup);
        builder.Services.AddSingleton(gate);
        builder.Services.AddHostedService<BlockingStartupService>();
        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var startTask = host.StartAsync(TestContext.Current.CancellationToken);

        await gate.Entered.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        application.StartupTiming!.IsReady.Should().BeFalse();
        startTask.IsCompleted.Should().BeFalse();

        gate.Release.TrySetResult();
        await startTask.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        try
        {
            application.StartupTiming.IsReady.Should().BeTrue();
        }
        finally
        {
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task StartupTiming_WhileConcurrentLifecycleServiceIsInStartedAsync_ShouldRemainPending()
    {
        var startup = MonicaStartup.Start();
        var gate = new StartupGate();
        var builder = Host.CreateApplicationBuilder();
        ConfigureMonica(builder, startup);
        builder.Services.Configure<HostOptions>(static options => options.ServicesStartConcurrently = true);
        builder.Services.AddSingleton(gate);
        builder.Services.AddHostedService<BlockingStartedLifecycleService>();
        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var startTask = host.StartAsync(TestContext.Current.CancellationToken);

        await gate.Entered.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        application.StartupTiming!.IsReady.Should().BeFalse();
        startTask.IsCompleted.Should().BeFalse();

        gate.Release.TrySetResult();
        await startTask.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        try
        {
            application.StartupTiming.IsReady.Should().BeTrue();
        }
        finally
        {
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task StartupTiming_WhenAHostedServiceFails_ShouldKeepCompositionFinalWithoutInventingReadiness()
    {
        var builder = Host.CreateApplicationBuilder();
        ConfigureMonica(builder, MonicaStartup.Start());
        builder.Services.AddHostedService<FailingStartupService>();
        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var diagnostics = host.Services.GetRequiredService<ModuleDiagnosticsFacade>();
        var beforeStart = diagnostics.GetSnapshot().Data!;

        Func<Task> start = () => host.StartAsync(TestContext.Current.CancellationToken);

        await start.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("host-start-failure");
        var afterFailure = diagnostics.GetSnapshot().Data!;
        afterFailure.IsFinal.Should().BeTrue();
        afterFailure.Summary.ApplicationStartupDurationMs.Should().BeNull();
        application.StartupTiming!.IsReady.Should().BeFalse();
        diagnostics.GetSnapshot().Data.Should().BeSameAs(afterFailure);
        afterFailure.Revision.Should().BeGreaterThanOrEqualTo(beforeStart.Revision);
    }

    [Fact]
    public async Task StartupTiming_WhenLifecycleStartedAsyncFails_ShouldRemainIncomplete()
    {
        var builder = Host.CreateApplicationBuilder();
        ConfigureMonica(builder, MonicaStartup.Start());
        builder.Services.Configure<HostOptions>(static options => options.ServicesStartConcurrently = true);
        builder.Services.AddHostedService<FailingStartedLifecycleService>();
        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var diagnostics = host.Services.GetRequiredService<ModuleDiagnosticsFacade>();

        Func<Task> start = () => host.StartAsync(TestContext.Current.CancellationToken);

        await start.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("lifecycle-started-failure");
        application.StartupTiming!.IsReady.Should().BeFalse();
        diagnostics.GetSnapshot().Data!.IsFinal.Should().BeTrue();
        diagnostics.GetSnapshot().Data!.Summary.ApplicationStartupDurationMs.Should().BeNull();
    }

    [Fact]
    public async Task ApplicationLifecycle_WhenDisposedBeforeStartedAsync_ShouldRejectLateRegistration()
    {
        var startup = MonicaStartup.Start();
        using var application = new MonicaApplication(startup.Claim());
        application.Profiling.StartModuleSystem();
        using var lifetime = new TestApplicationLifetime();
        var lifecycle = new MonicaApplicationLifecycle(application, lifetime);
        lifecycle.Dispose();

        Func<Task> started = () => lifecycle.StartedAsync(CancellationToken.None);

        await started.Should().ThrowAsync<ObjectDisposedException>();
        lifetime.NotifyStarted();
        application.StartupTiming!.IsReady.Should().BeFalse();
    }

    private static void ConfigureMonica(
        HostApplicationBuilder builder,
        MonicaStartup? startup = null)
    {
        static void Configure(IMonicaBuilder monica)
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModuleSystem();
        }

        if (startup is null)
        {
            builder.AddMonica(Configure);
            return;
        }

        builder.AddMonica(startup, Configure);
    }

    private sealed class StartupGate
    {
        internal TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class BlockingStartupService(StartupGate gate) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            gate.Entered.TrySetResult();
            await gate.Release.Task.WaitAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailingStartupService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException("host-start-failure"));

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class BlockingStartedLifecycleService(StartupGate gate) : IHostedLifecycleService
    {
        public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task StartedAsync(CancellationToken cancellationToken)
        {
            gate.Entered.TrySetResult();
            await gate.Release.Task.WaitAsync(cancellationToken);
        }

        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailingStartedLifecycleService : IHostedLifecycleService
    {
        public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StartedAsync(CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException("lifecycle-started-failure"));

        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => _stopped.Token;

        public void StopApplication() => _stopping.Cancel();

        internal void NotifyStarted() => _started.Cancel();

        public void Dispose()
        {
            _started.Dispose();
            _stopping.Dispose();
            _stopped.Dispose();
        }
    }
}
