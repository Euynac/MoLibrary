using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Diagnostics.Facades;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services.Support;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleDiagnosticsBlockingChainTests
{
    private static readonly TimeSpan HANG_GUARD = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task GetSnapshot_WhenMultipleItemsBlockOneBarrier_ShouldPreserveEveryCausalSegmentAndReleaser()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModuleSystem();
            monica.AddModule<DiagnosticsProviderModule, DiagnosticsProviderModuleOption>();
        });
        using var host = builder.Build();
        var application = host.Services.GetRequiredService<global::Monica.Core.MonicaApplication>();
        var profiler = application.Profiling;
        profiler.Clear();
        profiler.StartModuleSystem();
        var firstCompleted = new TaskCompletionSource<ModuleStartupWorkResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstCompletionSignaled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ModuleStartupWorkScheduler? schedulerReference = null;
        using var scheduler = new ModuleStartupWorkScheduler(
            maxConcurrency: 2,
            itemCompleted: result =>
            {
                if (result.Name == "first-blocker")
                {
                    firstCompleted.TrySetResult(result);
                }
            },
            stateChanged: () =>
            {
                profiler.RecordExternalMutation();
                var first = schedulerReference?.GetSnapshot().WorkItems.FirstOrDefault(static work =>
                    work.Name == "first-blocker");
                if (first?.CompletionSignalSequence.HasValue == true)
                {
                    firstCompletionSignaled.TrySetResult();
                }
            });
        schedulerReference = scheduler;
        profiler.AttachStartupWorkDiagnostics(scheduler.GetSnapshot);
        using var firstGate = new BarrierWorkGate();
        using var secondGate = new BarrierWorkGate();
        var barrierEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var moduleKey = ModuleKey.FromModuleType(typeof(DiagnosticsProviderModule));
        scheduler.Schedule(
            typeof(DiagnosticsProviderModule),
            moduleKey,
            registrationOrder: 1,
            "first-blocker",
            ModulePhase.ConfigureServices,
            ModuleStartupWorkBarrier.BeforeTypeDiscovery,
            firstGate.Run);
        scheduler.Schedule(
            typeof(DiagnosticsProviderModule),
            moduleKey,
            registrationOrder: 1,
            "barrier-releaser",
            ModulePhase.ConfigureServices,
            ModuleStartupWorkBarrier.BeforeTypeDiscovery,
            secondGate.Run,
            static () => { });
        Task<ModuleStartupWorkBarrierResult>? barrierTask = null;

        try
        {
            await Task.WhenAll(firstGate.Entered.Task, secondGate.Entered.Task)
                .WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            barrierTask = Task.Run(
                () => scheduler.ReachBarrier(
                    ModuleStartupWorkBarrier.BeforeTypeDiscovery,
                    _ => barrierEntered.TrySetResult()),
                TestContext.Current.CancellationToken);
            await barrierEntered.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

            firstGate.Release();
            var firstResult = await firstCompleted.Task.WaitAsync(
                HANG_GUARD,
                TestContext.Current.CancellationToken);
            await firstCompletionSignaled.Task.WaitAsync(
                HANG_GUARD,
                TestContext.Current.CancellationToken);
            secondGate.Release();
            var barrier = await barrierTask.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            var releasingWork = barrier.WorkItems.Single(work => work.Name == "barrier-releaser");
            var commit = releasingWork.Commit?.Take()
                ?? throw new InvalidOperationException("The barrier releaser did not retain its commit callback.");
            profiler.StartModulePhase(
                typeof(DiagnosticsProviderModule),
                moduleKey,
                registrationOrder: 1,
                ModulePhase.ConfigureServices,
                ModuleCallbackKind.StartupWorkCommit,
                releasingWork.WorkItemId);
            try
            {
                commit();
            }
            finally
            {
                profiler.StopModulePhase(typeof(DiagnosticsProviderModule), ModulePhase.ConfigureServices);
            }

            scheduler.Drain();
            profiler.RecordMilestone(ModuleCompositionMilestone.ServiceRegistrationCompleted);
            profiler.RecordMilestone(ModuleCompositionMilestone.CompositionCompleted);
            profiler.StopModuleSystem();

            var snapshotResult = host.Services.GetRequiredService<ModuleDiagnosticsFacade>().GetSnapshot();
            snapshotResult.Data.Should().NotBeNull();
            var snapshot = snapshotResult.Data!;
            var firstSegment = snapshot.BlockingChain.Single(segment =>
                segment.WorkItemId == firstResult.WorkItemId);
            var releasingSegment = snapshot.BlockingChain.Single(segment =>
                segment.WorkItemId == releasingWork.WorkItemId);

            snapshot.IsFinal.Should().BeTrue();
            snapshot.BlockingChain.Should().HaveCount(2);
            snapshot.BlockingChain.Should().OnlyContain(segment =>
                segment.Barrier == ModuleStartupWorkBarrier.BeforeTypeDiscovery
                && segment.ModuleKey == moduleKey
                && segment.BlockingDurationMs > 0
                && snapshot.TraceSpans.Any(span =>
                    span.SpanId == segment.BarrierSpanId
                    && span.Kind == ModuleDiagnosticsTraceSpanKind.StartupBarrier)
                && snapshot.TraceSpans.Any(span =>
                    span.SpanId == segment.WorkSpanId
                    && span.Kind == ModuleDiagnosticsTraceSpanKind.StartupWork
                    && span.WorkItemId == segment.WorkItemId));
            firstSegment.IsBarrierReleaser.Should().BeFalse();
            firstSegment.CommitSpanId.Should().BeNull();
            firstSegment.CommitDurationMs.Should().Be(0);
            releasingSegment.IsBarrierReleaser.Should().BeTrue();
            releasingSegment.BlockingDurationMs.Should().BeGreaterThanOrEqualTo(
                firstSegment.BlockingDurationMs);
            releasingSegment.CommitSpanId.Should().NotBeNull();
            var commitSpan = snapshot.TraceSpans.Single(span => span.SpanId == releasingSegment.CommitSpanId);
            commitSpan.Kind.Should().Be(ModuleDiagnosticsTraceSpanKind.ModuleCallback);
            commitSpan.CallbackKind.Should().Be(ModuleCallbackKind.StartupWorkCommit);
            commitSpan.WorkItemId.Should().Be(releasingSegment.WorkItemId);
            releasingSegment.CommitDurationMs.Should().Be(commitSpan.DurationMs);
            snapshot.BlockingChain.Should().ContainSingle(static segment => segment.IsBarrierReleaser);
        }
        finally
        {
            firstGate.Release();
            secondGate.Release();
            if (barrierTask is not null && !barrierTask.IsCompleted)
            {
                try
                {
                    await barrierTask.WaitAsync(HANG_GUARD, CancellationToken.None);
                }
                catch
                {
                    // Cleanup must not replace the scenario's original failure.
                }
            }
        }
    }

    private sealed class BarrierWorkGate : IDisposable
    {
        private readonly ManualResetEventSlim _release = new(initialState: false);

        internal TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Run()
        {
            Entered.TrySetResult();
            if (!_release.Wait(HANG_GUARD))
            {
                throw new TimeoutException("The blocking-chain work was not released.");
            }
        }

        internal void Release() => _release.Set();

        public void Dispose() => _release.Dispose();
    }
}
