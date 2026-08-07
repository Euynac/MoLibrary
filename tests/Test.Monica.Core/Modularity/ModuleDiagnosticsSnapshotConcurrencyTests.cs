using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Diagnostics.Facades;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Diagnostics.Services;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleDiagnosticsSnapshotConcurrencyTests
{
    private static readonly TimeSpan HANG_GUARD = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task GetSnapshot_WhenFirstTerminalProjectionIsConcurrent_ShouldPublishOneCachedInstance()
    {
        using var gate = new SnapshotConcurrencyWorkGate();
        using var host = CreateHost(gate);
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var facade = host.Services.GetRequiredService<ModuleDiagnosticsFacade>();
        await gate.Entered.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        gate.Release();
        application.Modules.DrainStartupWork();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = Enumerable.Range(0, 32).Select(async _ =>
        {
            await start.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            return facade.GetSnapshot();
        }).ToArray();

        start.SetResult();
        var results = await Task.WhenAll(calls);

        results.Should().OnlyContain(result => result.Status == ResStatus.Ok && result.Data != null);
        var snapshots = results.Select(static result => result.Data!).ToArray();
        snapshots.Should().OnlyContain(static snapshot =>
            snapshot.IsFinal && snapshot.Outcome == ModuleCompositionOutcome.Succeeded);
        snapshots.Should().OnlyContain(snapshot => ReferenceEquals(snapshot, snapshots[0]));
        snapshots.Select(static snapshot => snapshot.Revision).Should().OnlyContain(revision =>
            revision == snapshots[0].Revision);
    }

    [Fact]
    public async Task GetSnapshot_WhenLiveProjectionCompletesAfterTerminalPublication_ShouldReturnTerminalInstance()
    {
        using var gate = new SnapshotConcurrencyWorkGate();
        using var projectionGate = new SnapshotProjectionGate();
        using var host = CreateHost(gate);
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var facade = host.Services.GetRequiredService<ModuleDiagnosticsFacade>();
        var diagnostics = host.Services.GetRequiredService<ModuleDiagnosticsService>();
        await gate.Entered.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        var initial = facade.GetSnapshot().Data!;
        diagnostics.SetSnapshotProjectionObserver(projectionGate.WaitForRelease);
        try
        {
            var inFlightLiveProjection = Task.Run(
                () => facade.GetSnapshot().Data!,
                TestContext.Current.CancellationToken);
            await projectionGate.Entered.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

            gate.Release();
            application.Modules.DrainStartupWork();
            var terminal = facade.GetSnapshot().Data!;
            projectionGate.Release();
            var lateProjectionResult = await inFlightLiveProjection.WaitAsync(
                HANG_GUARD,
                TestContext.Current.CancellationToken);
            var repeated = facade.GetSnapshot().Data!;

            initial.IsFinal.Should().BeFalse();
            initial.Outcome.Should().BeNull();
            initial.Summary.ActiveStartupWorkCount.Should().Be(1);
            initial.TraceSpans.Should().ContainSingle(span =>
                span.Kind == ModuleDiagnosticsTraceSpanKind.StartupWork && !span.IsComplete);

            terminal.IsFinal.Should().BeTrue();
            terminal.Outcome.Should().Be(ModuleCompositionOutcome.Succeeded);
            terminal.Revision.Should().BeGreaterThan(initial.Revision);
            terminal.Summary.ActiveStartupWorkCount.Should().Be(0);
            terminal.TraceSpans.Should().ContainSingle(span =>
                span.Kind == ModuleDiagnosticsTraceSpanKind.StartupWork && span.IsComplete);
            terminal.CompositionId.Should().Be(initial.CompositionId);
            terminal.CapturedAtUtc.Should().BeOnOrAfter(initial.CapturedAtUtc);
            lateProjectionResult.Should().BeSameAs(terminal);
            repeated.Should().BeSameAs(terminal);
        }
        finally
        {
            projectionGate.Release();
            diagnostics.SetSnapshotProjectionObserver(null);
        }
    }

    private static IHost CreateHost(SnapshotConcurrencyWorkGate gate)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureModuleSystem(static options => options.MaxConcurrentStartupWorkItems = 2);
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModuleSystem();
            monica.AddModule<SnapshotConcurrencyProbeModule, SnapshotConcurrencyProbeOption>(options =>
                options.Gate = gate);
        });
        return builder.Build();
    }
}

internal sealed class SnapshotProjectionGate : IDisposable
{
    private readonly ManualResetEventSlim _release = new(initialState: false);
    private int _claimed;

    internal TaskCompletionSource Entered { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void WaitForRelease()
    {
        if (Interlocked.Exchange(ref _claimed, 1) != 0)
        {
            return;
        }

        Entered.TrySetResult();
        if (!_release.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("The controlled diagnostics projection was not released.");
        }
    }

    internal void Release() => _release.Set();

    public void Dispose() => _release.Dispose();
}

internal sealed class SnapshotConcurrencyProbeModule : MonicaModule<SnapshotConcurrencyProbeOption>
{
    public override void ConfigureServices(ModuleContext<SnapshotConcurrencyProbeOption> context)
    {
        ScheduleStartupWork(
            "snapshot-concurrency-probe",
            Option.Gate.Run,
            ModuleStartupWorkBarrier.NoBarrier);
    }
}

internal sealed class SnapshotConcurrencyProbeOption : ModuleOptions<SnapshotConcurrencyProbeModule>
{
    internal SnapshotConcurrencyWorkGate Gate { get; set; } = null!;
}

internal sealed class SnapshotConcurrencyWorkGate : IDisposable
{
    private readonly ManualResetEventSlim _release = new(initialState: false);

    internal TaskCompletionSource Entered { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void Run()
    {
        Entered.TrySetResult();
        if (!_release.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("The controlled snapshot-concurrency work was not released.");
        }
    }

    internal void Release() => _release.Set();

    public void Dispose() => _release.Dispose();
}
