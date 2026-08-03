using System.Diagnostics;
using System.Runtime.ExceptionServices;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Diagnostics.Services;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services.Support;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleStartupWorkTests
{
    private static readonly TimeSpan HANG_GUARD = TimeSpan.FromSeconds(10);

    [Fact]
    public void BarrierContract_ShouldContainRequiredAndNonBlockingStartupPolicies()
    {
        Enum.GetNames<ModuleStartupWorkBarrier>().Should().Equal(
            nameof(ModuleStartupWorkBarrier.BeforeBusinessTypeIteration),
            nameof(ModuleStartupWorkBarrier.BeforePostConfigureServices),
            nameof(ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion),
            nameof(ModuleStartupWorkBarrier.BeforeHostLifecycle),
            nameof(ModuleStartupWorkBarrier.NoBarrier));
    }

    [Fact]
    public void Diagnostics_AfterStartupWorkCompletes_ShouldKeepSerialAndWorkerDurationsSeparate()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModuleSystem();
            AddProbeOne(monica, options => options.AddConfigureServicesWork(
                "diagnostic-work",
                static () => { }));
        });

        using var host = builder.Build();
        var performance = host.Services.GetRequiredService<IModuleSystemInspectionService>()
            .GetSystemPerformance();
        var module = performance.Modules.Single(item =>
            item.ModuleTypeName == nameof(StartupWorkProbeModuleOne));
        var work = module.StartupWorkItems.Should().ContainSingle().Subject;
        var composition = performance.Composition;

        composition.StartupWorkItems.Should().ContainSingle();
        composition.StartupWorkBarriers.Select(static checkpoint => checkpoint.Barrier).Should().Equal(
            ModuleStartupWorkBarrier.BeforeBusinessTypeIteration,
            ModuleStartupWorkBarrier.BeforePostConfigureServices,
            ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion);
        module.SerialDurationMs.Should().Be(module.PhaseExecutions.Sum(static execution => execution.DurationMs));
        module.PhaseExecutions.Should().Contain(static execution =>
            execution.Phase == ModulePhase.IterateBusinessTypes);
        composition.AggregateSerialModuleDurationMs.Should()
            .Be(composition.ModulePhaseExecutions.Sum(static execution => execution.DurationMs));
        work.Name.Should().Be("diagnostic-work");
        work.ModuleKey.Should().Be(module.ModuleKey);
        work.ModuleTypeName.Should().Be(module.ModuleTypeName);
        work.ModuleRegistrationOrder.Should().Be(module.RegistrationOrder);
        work.OriginPhase.Should().Be(ModulePhase.ConfigureServices);
        work.Barrier.Should().Be(ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion);
        work.Status.Should().Be(ModuleStartupWorkStatus.Succeeded);
        work.StartedOffsetMs.Should().NotBeNull();
        work.CompletedOffsetMs.Should().NotBeNull();
        work.StartedAtUtc.Should().NotBeNull();
        work.CompletedAtUtc.Should().NotBeNull();
        work.SubmittedOffsetMs.Should().BeLessThanOrEqualTo(work.StartedOffsetMs!.Value);
        work.StartedOffsetMs.Value.Should().BeLessThanOrEqualTo(work.CompletedOffsetMs!.Value);
        work.SubmittedAtUtc.Should().BeOnOrBefore(work.StartedAtUtc!.Value);
        work.StartedAtUtc.Value.Should().BeOnOrBefore(work.CompletedAtUtc!.Value);
        composition.SystemPhases.Should().OnlyContain(phase =>
            phase.StartedOffsetMs >= 0
            && phase.StartedOffsetMs <= phase.CompletedOffsetMs
            && phase.CompletedOffsetMs <= composition.ElapsedDurationMs);
        composition.ModulePhaseExecutions.Should().OnlyContain(execution =>
            execution.StartedOffsetMs >= 0
            && execution.StartedOffsetMs <= execution.CompletedOffsetMs
            && execution.CompletedOffsetMs <= composition.ElapsedDurationMs);
        composition.StartupWorkItems.Should().OnlyContain(item =>
            item.SubmittedOffsetMs >= 0
            && item.StartedOffsetMs.HasValue
            && item.CompletedOffsetMs.HasValue
            && item.SubmittedOffsetMs <= item.StartedOffsetMs.Value
            && item.StartedOffsetMs.Value <= item.CompletedOffsetMs.Value
            && item.CompletedOffsetMs.Value <= composition.ElapsedDurationMs);
        composition.StartupWorkBarriers.Should().OnlyContain(checkpoint =>
            checkpoint.EnteredOffsetMs >= 0
            && checkpoint.EnteredOffsetMs <= checkpoint.ReleasedOffsetMs
            && checkpoint.ReleasedOffsetMs <= composition.ElapsedDurationMs);
    }

    [Fact]
    public void Diagnostics_WhenStartupWorkFails_ShouldPreserveTheTerminalFailureWithinTheTimeline()
    {
        var profiler = new ModuleInitializationProfiler();
        profiler.StartModuleSystem();
        using var scheduler = new ModuleStartupWorkScheduler(maxConcurrency: 1);
        scheduler.Schedule(
            typeof(StartupWorkProbeModuleOne),
            ModuleKey.Create("Test.Monica.FailedStartupWork"),
            registrationOrder: 1,
            "failed-work",
            ModulePhase.ConfigureServices,
            ModuleStartupWorkBarrier.BeforeBusinessTypeIteration,
            static () => throw new InvalidOperationException("diagnostic-work-failure"));

        var checkpoint = scheduler.ReachBarrier(
            ModuleStartupWorkBarrier.BeforeBusinessTypeIteration);
        checkpoint.HasFailures.Should().BeTrue();
        scheduler.Drain();
        profiler.AttachStartupWorkDiagnostics(scheduler.GetSnapshot);
        profiler.StopModuleSystem();

        var composition = profiler.GetCompositionPerformance();
        var failed = composition.StartupWorkItems.Should().ContainSingle().Subject;
        failed.Status.Should().Be(ModuleStartupWorkStatus.Failed);
        failed.ErrorMessage.Should().Contain("diagnostic-work-failure");
        failed.SubmittedOffsetMs.Should().BeGreaterThanOrEqualTo(0);
        failed.StartedOffsetMs.Should().NotBeNull();
        failed.CompletedOffsetMs.Should().NotBeNull();
        failed.SubmittedOffsetMs.Should().BeLessThanOrEqualTo(failed.StartedOffsetMs!.Value);
        failed.StartedOffsetMs.Value.Should().BeLessThanOrEqualTo(failed.CompletedOffsetMs!.Value);
        failed.CompletedOffsetMs.Value.Should().BeLessThanOrEqualTo(composition.ElapsedDurationMs);
    }

    [Fact]
    public async Task Diagnostics_WhenBarrierHasCompletedAndPendingWork_ShouldIdentifyExactBarrierReleaser()
    {
        var completedBeforeCheckpoint = NewSignal();
        var checkpointEntered = NewSignal();
        using var blocker = new WorkGate(expectedEntrants: 1);
        var profiler = new ModuleInitializationProfiler();
        profiler.StartModuleSystem();
        using var scheduler = new ModuleStartupWorkScheduler(maxConcurrency: 1);
        var moduleKey = ModuleKey.Create("Test.Monica.StartupWork");
        scheduler.Schedule(
            typeof(StartupWorkProbeModuleOne),
            moduleKey,
            1,
            "completed-before-checkpoint",
            ModulePhase.ConfigureServices,
            ModuleStartupWorkBarrier.BeforeBusinessTypeIteration,
            () => completedBeforeCheckpoint.TrySetResult());
        scheduler.Schedule(
            typeof(StartupWorkProbeModuleOne),
            moduleKey,
            1,
            "checkpoint-releaser",
            ModulePhase.ConfigureServices,
            ModuleStartupWorkBarrier.BeforeBusinessTypeIteration,
            blocker.Run);
        Task<ModuleStartupWorkBarrierResult>? checkpoint = null;

        try
        {
            await blocker.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            checkpoint = Task.Run(
                () => scheduler.ReachBarrier(
                    ModuleStartupWorkBarrier.BeforeBusinessTypeIteration,
                    _ => checkpointEntered.TrySetResult()),
                TestContext.Current.CancellationToken);
            await checkpointEntered.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            completedBeforeCheckpoint.Task.IsCompleted.Should().BeTrue();

            blocker.Release();
            var result = await checkpoint.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            var completed = result.WorkItems.Single(item => item.Name == "completed-before-checkpoint");
            var releaser = result.WorkItems.Single(item => item.Name == "checkpoint-releaser");

            result.WorkItems.Select(static item => item.WorkItemId)
                .Should().Equal(completed.WorkItemId, releaser.WorkItemId);
            result.PendingWorkItems.Should().ContainSingle()
                .Which.WorkItemId.Should().Be(releaser.WorkItemId);
            result.ReleasingWorkItemId.Should().Be(releaser.WorkItemId);
            result.PendingWorkItems[0].RemainingDuration.Should().BeGreaterThan(TimeSpan.Zero);

            profiler.AttachStartupWorkDiagnostics(scheduler.GetSnapshot);
            profiler.StopModuleSystem();
            var composition = profiler.GetCompositionPerformance();
            var completedPerformance = composition.StartupWorkItems.Single(item =>
                item.Name == "completed-before-checkpoint");
            var releaserPerformance = composition.StartupWorkItems.Single(item =>
                item.Name == "checkpoint-releaser");

            completedPerformance.WasPendingAtBarrier.Should().BeFalse();
            completedPerformance.IsBarrierReleaser.Should().BeFalse();
            releaserPerformance.WasPendingAtBarrier.Should().BeTrue();
            releaserPerformance.IsBarrierReleaser.Should().BeTrue();
            releaserPerformance.RemainingAtBarrierMs.Should().BeGreaterThan(0);
            composition.CriticalBarrier?.ReleasingWorkItemId.Should().Be(releaser.WorkItemId);
            composition.CriticalWorkItem?.WorkItemId.Should().Be(releaser.WorkItemId);
        }
        finally
        {
            blocker.Release();
            if (checkpoint is not null)
            {
                await ObserveCompositionCompletionAsync(checkpoint);
            }
        }
    }

    [Fact]
    public async Task Diagnostics_WhenCompletionObserversReorderSignals_ShouldIdentifyTheActualBarrierReleaser()
    {
        using var firstObserverGate = new WorkGate(expectedEntrants: 1);
        using var secondWorkGate = new WorkGate(expectedEntrants: 1);
        var barrierEntered = NewSignal();
        using var scheduler = new ModuleStartupWorkScheduler(
            maxConcurrency: 2,
            itemCompleted: result =>
            {
                if (result.Name == "first-work")
                {
                    firstObserverGate.Run();
                }
            });
        var moduleKey = ModuleKey.Create("Test.Monica.SignalOrderedStartupWork");
        scheduler.Schedule(
            typeof(StartupWorkProbeModuleOne),
            moduleKey,
            1,
            "first-work",
            ModulePhase.ConfigureServices,
            ModuleStartupWorkBarrier.BeforeBusinessTypeIteration,
            static () => { });
        scheduler.Schedule(
            typeof(StartupWorkProbeModuleOne),
            moduleKey,
            1,
            "second-work",
            ModulePhase.ConfigureServices,
            ModuleStartupWorkBarrier.BeforeBusinessTypeIteration,
            secondWorkGate.Run);
        Task<ModuleStartupWorkBarrierResult>? barrier = null;

        try
        {
            await firstObserverGate.ExpectedEntrantsReached.WaitAsync(
                HANG_GUARD,
                TestContext.Current.CancellationToken);
            await secondWorkGate.ExpectedEntrantsReached.WaitAsync(
                HANG_GUARD,
                TestContext.Current.CancellationToken);
            barrier = Task.Run(
                () => scheduler.ReachBarrier(
                    ModuleStartupWorkBarrier.BeforeBusinessTypeIteration,
                    _ => barrierEntered.TrySetResult()),
                TestContext.Current.CancellationToken);
            await barrierEntered.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

            secondWorkGate.Release();
            SpinWait.SpinUntil(
                    () => scheduler.GetSnapshot().WorkItems.Single(item => item.Name == "second-work")
                        .CompletionSignalSequence.HasValue,
                    HANG_GUARD)
                .Should().BeTrue();
            firstObserverGate.Release();

            var result = await barrier.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            var first = result.WorkItems.Single(item => item.Name == "first-work");
            var second = result.WorkItems.Single(item => item.Name == "second-work");

            first.CompletedTimestamp.Should().NotBeNull();
            second.CompletedTimestamp.Should().NotBeNull();
            first.CompletionSignalSequence.Should().NotBeNull();
            second.CompletionSignalSequence.Should().NotBeNull();
            first.CompletedTimestamp!.Value.Should().BeLessThan(second.CompletedTimestamp!.Value);
            first.CompletionSignalSequence!.Value.Should().BeGreaterThan(second.CompletionSignalSequence!.Value);
            result.ReleasingWorkItemId.Should().Be(first.WorkItemId);
        }
        finally
        {
            secondWorkGate.Release();
            firstObserverGate.Release();
            if (barrier is not null)
            {
                await ObserveCompositionCompletionAsync(barrier);
            }
        }
    }

    [Fact]
    public void Diagnostics_WhenRegistrationHasNoRuntimeSnapshot_ShouldPreserveItsCallbackSpans()
    {
        var profiler = new ModuleInitializationProfiler();
        var moduleKey = ModuleKey.Create("Test.Monica.DisabledComposition");
        profiler.StartModuleSystem();
        profiler.StartModulePhase(
            typeof(StartupWorkProbeModuleOne),
            moduleKey,
            registrationOrder: 42,
            phase: ModulePhase.ClaimDependencies);
        profiler.StopModulePhase(typeof(StartupWorkProbeModuleOne), ModulePhase.ClaimDependencies);
        profiler.StopModuleSystem();

        var composition = profiler.GetCompositionPerformance();
        var module = profiler.GetModulePerformances(new HashSet<ModuleKey>()).Should().ContainSingle().Subject;

        composition.ModulePhaseExecutions.Should().ContainSingle(execution =>
            execution.ModuleKey == moduleKey
            && execution.ModuleRegistrationOrder == 42
            && execution.Phase == ModulePhase.ClaimDependencies);
        module.ModuleKey.Should().Be(moduleKey);
        module.IsRuntimeAvailable.Should().BeFalse();
        module.PhaseExecutions.Should().ContainSingle();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartAsync_WhenWorkTargetsHostLifecycleBarrier_ShouldWaitBeforeLifecycleParticipants(
        bool useWebHost)
    {
        using var gate = new WorkGate(expectedEntrants: 1);
        var lifecycle = new StartupTrackingLifecycleService();
        var builder = CreateBuilder(useWebHost);
        builder.Services.Configure<HostOptions>(static options => options.ServicesStartConcurrently = true);
        builder.Services.AddSingleton<IHostedService>(lifecycle);
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddConfigureServicesWork(
                "host-lifecycle-work",
                gate.Run,
                ModuleStartupWorkBarrier.BeforeHostLifecycle));
        });
        using var host = BuildHost(builder, useWebHost);
        Task? start = null;

        try
        {
            await gate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            start = Task.Run(
                () => host.StartAsync(TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken);
            start.IsCompleted.Should().BeFalse();
            lifecycle.StartingCount.Should().Be(0);
            lifecycle.StartCount.Should().Be(0);

            gate.Release();
            await start.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            lifecycle.StartingCount.Should().Be(1);
            lifecycle.StartCount.Should().Be(1);
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            gate.Release();
            if (start is not null)
            {
                await ObserveCompositionCompletionAsync(start);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartAsync_WhenHostLifecycleWorkFails_ShouldExposeWorkNameAndUnderlyingFailure(
        bool useWebHost)
    {
        var lifecycle = new StartupTrackingLifecycleService();
        var builder = CreateBuilder(useWebHost);
        builder.Services.Configure<HostOptions>(static options => options.ServicesStartConcurrently = true);
        builder.Services.AddSingleton<IHostedService>(lifecycle);
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddConfigureServicesWork(
                "strict-host-work",
                static () => throw new InvalidOperationException("strict-host-work-failure"),
                ModuleStartupWorkBarrier.BeforeHostLifecycle));
        });
        using var host = BuildHost(builder, useWebHost);

        Func<Task> start = () => host.StartAsync(TestContext.Current.CancellationToken);

        await start.Should().ThrowAsync<OptionsValidationException>()
            .WithMessage("*strict-host-work*strict-host-work-failure*");
        lifecycle.StartingCount.Should().Be(0);
        lifecycle.StartCount.Should().Be(0);
    }

    [Fact]
    public async Task StartupValidation_WhenCalledConcurrently_ShouldReleaseHostBarrierOnce()
    {
        using var gate = new WorkGate(expectedEntrants: 1);
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddConfigureServicesWork(
                "coalesced-host-work",
                gate.Run,
                ModuleStartupWorkBarrier.BeforeHostLifecycle));
        });
        using var host = builder.Build();
        var application = host.Services.GetRequiredService<global::Monica.Core.MonicaApplication>();
        var firstValidator = new ModuleStartupValidator(application);
        var secondValidator = new ModuleStartupValidator(application);
        var options = new ModuleStartupValidationOptions();
        Task<ValidateOptionsResult>? first = null;
        Task<ValidateOptionsResult>? second = null;

        try
        {
            await gate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            first = Task.Run(() => firstValidator.Validate(null, options), TestContext.Current.CancellationToken);
            second = Task.Run(() => secondValidator.Validate(null, options), TestContext.Current.CancellationToken);
            first.IsCompleted.Should().BeFalse();
            second.IsCompleted.Should().BeFalse();

            gate.Release();
            (await first.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken)).Succeeded.Should().BeTrue();
            (await second.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken)).Succeeded.Should().BeTrue();
            application.Profiling.GetCompositionPerformance().StartupWorkBarriers
                .Should().ContainSingle(barrier => barrier.Barrier == ModuleStartupWorkBarrier.BeforeHostLifecycle);
            gate.InvocationCount.Should().Be(1);
        }
        finally
        {
            gate.Release();
            if (first is not null)
            {
                await ObserveCompositionCompletionAsync(first);
            }

            if (second is not null)
            {
                await ObserveCompositionCompletionAsync(second);
            }
        }
    }

    [Fact]
    public async Task NoBarrierWork_WhenStillRunning_ShouldNotDelayCompositionBuildOrHostStart()
    {
        using var gate = new WorkGate(expectedEntrants: 1);
        var builder = Host.CreateApplicationBuilder();
        var composition = Task.Run(() => builder.AddMonica(monica =>
        {
            monica.ConfigureModuleSystem(options => options.MaxConcurrentStartupWorkItems = 2);
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddConfigureServicesWork(
                "non-blocking-work",
                gate.Run,
                ModuleStartupWorkBarrier.NoBarrier));
        }), TestContext.Current.CancellationToken);
        IHost? host = null;

        try
        {
            await gate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            await composition.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            host = builder.Build();
            await host.StartAsync(TestContext.Current.CancellationToken)
                .WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

            var application = host.Services.GetRequiredService<global::Monica.Core.MonicaApplication>();
            var performance = application.Profiling.GetCompositionPerformance();
            performance.StartupWorkItems
                .Should().ContainSingle(work =>
                    work.Name == "non-blocking-work" && work.Status == ModuleStartupWorkStatus.Running);
            performance.StartupWorkBarriers.Should().NotContain(barrier =>
                barrier.Barrier == ModuleStartupWorkBarrier.NoBarrier);
            performance.AggregateBarrierWaitDurationMs.Should().Be(0);
        }
        finally
        {
            gate.Release();
            await ObserveCompositionCompletionAsync(composition);
            if (host is not null)
            {
                await host.StopAsync(TestContext.Current.CancellationToken);
                host.Dispose();
            }
        }
    }

    [Fact]
    public async Task AddMonica_WithOneLane_ShouldReserveItForRequiredWorkSubmittedAfterNoBarrierWork()
    {
        using var noBarrierGate = new WorkGate(expectedEntrants: 1);
        var requiredCompleted = NewSignal();
        var builder = Host.CreateApplicationBuilder();
        var composition = Task.Run(() => builder.AddMonica(monica =>
        {
            monica.ConfigureModuleSystem(options => options.MaxConcurrentStartupWorkItems = 1);
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options =>
            {
                options.AddConfigureServicesWork(
                    "submitted-first-non-blocking-work",
                    noBarrierGate.Run,
                    ModuleStartupWorkBarrier.NoBarrier);
                options.AddConfigureServicesWork(
                    "submitted-later-required-work",
                    () => requiredCompleted.TrySetResult(),
                    ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion);
            });
        }), TestContext.Current.CancellationToken);

        try
        {
            await requiredCompleted.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            await noBarrierGate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            await composition.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

            var host = builder.Build();
            try
            {
                var performance = host.Services
                    .GetRequiredService<global::Monica.Core.MonicaApplication>()
                    .Profiling.GetCompositionPerformance();
                performance.StartupWorkItems.Single(work => work.Name == "submitted-later-required-work")
                    .Status.Should().Be(ModuleStartupWorkStatus.Succeeded);
                performance.StartupWorkItems.Single(work => work.Name == "submitted-first-non-blocking-work")
                    .Status.Should().Be(ModuleStartupWorkStatus.Running);
            }
            finally
            {
                noBarrierGate.Release();
                host.Dispose();
            }
        }
        finally
        {
            noBarrierGate.Release();
            await ObserveCompositionCompletionAsync(composition);
        }
    }

    [Fact]
    public async Task Diagnostics_WhenWorkIsRunningAndQueued_ShouldPublishLiveThenTerminalStates()
    {
        using var gate = new WorkGate(expectedEntrants: 1);
        var profiler = new ModuleInitializationProfiler();
        profiler.StartModuleSystem();
        using var scheduler = new ModuleStartupWorkScheduler(maxConcurrency: 2);
        profiler.AttachStartupWorkDiagnostics(scheduler.GetSnapshot);
        var moduleKey = ModuleKey.Create("Test.Monica.LiveStartupWork");
        scheduler.Schedule(
            typeof(StartupWorkProbeModuleOne),
            moduleKey,
            1,
            "running-work",
            ModulePhase.ConfigureServices,
            ModuleStartupWorkBarrier.NoBarrier,
            gate.Run);
        scheduler.Schedule(
            typeof(StartupWorkProbeModuleOne),
            moduleKey,
            1,
            "queued-work",
            ModulePhase.ConfigureServices,
            ModuleStartupWorkBarrier.NoBarrier,
            static () => throw new InvalidOperationException("queued-work-failure"));

        await gate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        var live = profiler.GetCompositionPerformance().StartupWorkItems;
        live.Single(work => work.Name == "running-work").Status.Should().Be(ModuleStartupWorkStatus.Running);
        live.Single(work => work.Name == "queued-work").Status.Should().Be(ModuleStartupWorkStatus.Queued);
        live.Single(work => work.Name == "queued-work").StartedAtUtc.Should().BeNull();
        live.Single(work => work.Name == "queued-work").CompletedAtUtc.Should().BeNull();

        gate.Release();
        scheduler.Drain();
        profiler.StopModuleSystem();
        var terminal = profiler.GetCompositionPerformance().StartupWorkItems;
        terminal.Single(work => work.Name == "running-work").Status.Should().Be(ModuleStartupWorkStatus.Succeeded);
        terminal.Single(work => work.Name == "queued-work").Status.Should().Be(ModuleStartupWorkStatus.Failed);
        terminal.Single(work => work.Name == "queued-work").ErrorMessage.Should().Contain("queued-work-failure");
    }

    [Fact]
    public async Task Diagnostics_WhenNoBarrierWorkCompletesAfterComposition_ShouldUseObservationBoundary()
    {
        using var gate = new WorkGate(expectedEntrants: 1);
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureModuleSystem(options => options.MaxConcurrentStartupWorkItems = 2);
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddConfigureServicesWork(
                "late-diagnostic-work",
                gate.Run,
                ModuleStartupWorkBarrier.NoBarrier));
        });
        using var host = builder.Build();
        var application = host.Services.GetRequiredService<global::Monica.Core.MonicaApplication>();

        await gate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        var duringComposition = application.Profiling.GetCompositionPerformance();
        duringComposition.StartupWorkItems.Should().ContainSingle(work =>
            work.Name == "late-diagnostic-work" && work.Status == ModuleStartupWorkStatus.Running);

        gate.Release();
        var completed = await WaitForStartupWorkStatusAsync(
            application,
            "late-diagnostic-work",
            ModuleStartupWorkStatus.Succeeded);
        var observed = application.Profiling.GetCompositionPerformance();

        completed.CompletedOffsetMs.Should().NotBeNull();
        completed.CompletedOffsetMs!.Value.Should().BeGreaterThan(observed.ElapsedDurationMs);
        completed.CompletedOffsetMs.Value.Should().BeLessThanOrEqualTo(observed.ObservedDurationMs);
        completed.CompletedAtUtc.Should().NotBeNull();
        completed.CompletedAtUtc!.Value.Should().BeOnOrBefore(observed.ObservedAtUtc);
        observed.ObservedDurationMs.Should().BeGreaterThanOrEqualTo(observed.ElapsedDurationMs);
    }

    [Fact]
    public async Task NoBarrierFailure_WhenHostStarts_ShouldRemainDiagnosticOnly()
    {
        var attempted = NewSignal();
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddConfigureServicesWork(
                "nonfatal-work",
                () =>
                {
                    attempted.TrySetResult();
                    throw new InvalidOperationException("nonfatal-work-failure");
                },
                ModuleStartupWorkBarrier.NoBarrier));
        });
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await attempted.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        var application = host.Services.GetRequiredService<global::Monica.Core.MonicaApplication>();
        var failed = await WaitForStartupWorkStatusAsync(
            application,
            "nonfatal-work",
            ModuleStartupWorkStatus.Failed);
        failed.ErrorMessage.Should().Contain("nonfatal-work-failure");
        application.Modules.RegistrationErrors.Should().NotContain(error =>
            error.ErrorType == global::Monica.Core.Modularity.Models.Internal.ModuleRegistrationErrorType.StartupWorkError);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StopAsync_WhenNoBarrierWorkIsRunning_ShouldDrainItExactlyOnce()
    {
        using var gate = new WorkGate(expectedEntrants: 1);
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddConfigureServicesWork(
                "stop-drain-work",
                gate.Run,
                ModuleStartupWorkBarrier.NoBarrier));
        });
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        await gate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

        var stop = Task.Run(
            () => host.StopAsync(TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        stop.IsCompleted.Should().BeFalse();
        gate.Release();
        await stop.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        gate.InvocationCount.Should().Be(1);
        gate.CompletedCount.Should().Be(1);
    }

    [Fact]
    public async Task Dispose_WhenHostNeverStarted_ShouldDrainNoBarrierWorkExactlyOnce()
    {
        using var gate = new WorkGate(expectedEntrants: 1);
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddConfigureServicesWork(
                "dispose-drain-work",
                gate.Run,
                ModuleStartupWorkBarrier.NoBarrier));
        });
        var host = builder.Build();
        await gate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

        var dispose = Task.Run(host.Dispose, TestContext.Current.CancellationToken);
        dispose.IsCompleted.Should().BeFalse();
        gate.Release();
        await dispose.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        gate.InvocationCount.Should().Be(1);
        gate.CompletedCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_WhenHostNeverStarted_ShouldDisposeHostOwnedMonicaApplication()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, static _ => { });
        });
        var application = ((IHostApplicationBuilder)builder).Properties.Values
            .OfType<global::Monica.Core.MonicaApplication>()
            .Should().ContainSingle().Subject;
        var host = builder.Build();

        host.Dispose();

        Action createLogger = () => application.CreateLogger<ModuleStartupWorkTests>();
        createLogger.Should().Throw<ObjectDisposedException>();
    }

    [Theory]
    [InlineData(ModuleStartupWorkBarrier.BeforeHostLifecycle)]
    [InlineData(ModuleStartupWorkBarrier.NoBarrier)]
    public void AddMonica_WhenLateBarrierHasSerialCommit_ShouldRejectIt(ModuleStartupWorkBarrier barrier)
    {
        var builder = Host.CreateApplicationBuilder();

        Action add = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddBusinessTypeIterationWork(
                "invalid-late-commit",
                static () => { },
                static () => { },
                barrier));
        });

        var failure = add.Should().Throw<Exception>()
            .WithMessage("*Business-type iteration failed during Monica module registration*")
            .Which;
        failure.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Match(
                $"*{barrier}*cannot have a serial commit*service registration is sealed*");
    }

    [Fact]
    public void AddMonica_WhenStartupWorkersStart_ShouldNotFlowTheCallersExecutionContext()
    {
        var ambient = new AsyncLocal<string?> { Value = "composition-caller" };
        string? observedAmbient = "work-not-run";
        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.AddMonica(monica =>
            {
                monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
                AddProbeOne(monica, options => options.AddConfigureServicesWork(
                    "execution-context-probe",
                    () => observedAmbient = ambient.Value));
            });

            observedAmbient.Should().BeNull();
        }
        finally
        {
            ambient.Value = null;
        }
    }

    [Fact]
    public void AddMonica_WhenExecutionContextFlowIsAlreadySuppressed_ShouldStillRunStartupWork()
    {
        var workRan = false;
        var builder = Host.CreateApplicationBuilder();

        using (ExecutionContext.SuppressFlow())
        {
            builder.AddMonica(monica =>
            {
                monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
                AddProbeOne(monica, options => options.AddConfigureServicesWork(
                    "suppressed-context-work",
                    () => workRan = true));
            });
        }

        workRan.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddMonica_WhenTerminalWorkBlocks_ShouldWaitExactlyOnceForGenericAndWebHosts(bool useWebHost)
    {
        using var gate = new WorkGate(expectedEntrants: 1);
        var postConfigureReached = NewSignal();
        var builder = CreateBuilder(useWebHost);
        var composition = Task.Run(
            () => builder.AddMonica(monica =>
            {
                monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
                AddProbeOne(monica, options =>
                {
                    options.AddConfigureBuilderWork("terminal-work", gate.Run);
                    options.PostConfigureReached = () => postConfigureReached.TrySetResult();
                });
            }),
            TestContext.Current.CancellationToken);

        try
        {
            await gate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            await postConfigureReached.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

            composition.IsCompleted.Should().BeFalse();
            gate.InvocationCount.Should().Be(1);

            gate.Release();
            await composition.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

            gate.InvocationCount.Should().Be(1);
            gate.CompletedCount.Should().Be(1);
            await ExerciseHostBoundaryAsync(builder, useWebHost);
            gate.InvocationCount.Should().Be(1);
        }
        finally
        {
            gate.Release();
            await ObserveCompositionCompletionAsync(composition);
        }
    }

    [Fact]
    public async Task AddMonica_WhenWorkIsDueBeforeBusinessTypeIteration_ShouldHoldThatCheckpoint()
    {
        using var gate = new WorkGate(expectedEntrants: 1);
        var iterationReached = NewSignal();
        var builder = Host.CreateApplicationBuilder();
        var composition = Task.Run(
            () => builder.AddMonica(monica =>
            {
                monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
                AddProbeOne(monica, options =>
                {
                    options.AddConfigureServicesWork(
                        "before-business-types",
                        gate.Run,
                        ModuleStartupWorkBarrier.BeforeBusinessTypeIteration);
                    options.BusinessTypeIterationReached = () => iterationReached.TrySetResult();
                });
            }),
            TestContext.Current.CancellationToken);

        try
        {
            await gate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

            iterationReached.Task.IsCompleted.Should().BeFalse();
            composition.IsCompleted.Should().BeFalse();

            gate.Release();
            await iterationReached.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            await composition.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        }
        finally
        {
            gate.Release();
            await ObserveCompositionCompletionAsync(composition);
        }
    }

    [Fact]
    public async Task AddMonica_WhenWorkIsDueBeforePostConfigureServices_ShouldAllowIterationButHoldPostConfigure()
    {
        using var gate = new WorkGate(expectedEntrants: 1);
        var iterationReached = NewSignal();
        var postConfigureReached = NewSignal();
        var builder = Host.CreateApplicationBuilder();
        var composition = Task.Run(
            () => builder.AddMonica(monica =>
            {
                monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
                AddProbeOne(monica, options =>
                {
                    options.AddConfigureServicesWork(
                        "before-post-configure",
                        gate.Run,
                        ModuleStartupWorkBarrier.BeforePostConfigureServices);
                    options.BusinessTypeIterationReached = () => iterationReached.TrySetResult();
                    options.PostConfigureReached = () => postConfigureReached.TrySetResult();
                });
            }),
            TestContext.Current.CancellationToken);

        try
        {
            await gate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            await iterationReached.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

            postConfigureReached.Task.IsCompleted.Should().BeFalse();
            composition.IsCompleted.Should().BeFalse();

            gate.Release();
            await postConfigureReached.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            await composition.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        }
        finally
        {
            gate.Release();
            await ObserveCompositionCompletionAsync(composition);
        }
    }

    [Fact]
    public async Task AddMonica_WhenOneModuleSchedulesFourItemsWithLimitTwo_ShouldBoundWorkItemConcurrency()
    {
        using var gate = new WorkGate(expectedEntrants: 2);
        var builder = Host.CreateApplicationBuilder();
        var composition = Task.Run(
            () => builder.AddMonica(monica =>
            {
                monica.ConfigureModuleSystem(options => options.MaxConcurrentStartupWorkItems = 2);
                monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
                AddProbeOne(monica, options =>
                {
                    options.AddConfigureServicesWork("work-1", gate.Run);
                    options.AddConfigureServicesWork("work-2", gate.Run);
                    options.AddConfigureServicesWork("work-3", gate.Run);
                    options.AddConfigureServicesWork("work-4", gate.Run);
                });
            }),
            TestContext.Current.CancellationToken);

        try
        {
            await gate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

            composition.IsCompleted.Should().BeFalse();
            gate.InvocationCount.Should().Be(2);
            gate.MaximumConcurrency.Should().Be(2);

            gate.Release();
            await composition.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

            gate.InvocationCount.Should().Be(4);
            gate.CompletedCount.Should().Be(4);
            gate.MaximumConcurrency.Should().Be(2);
        }
        finally
        {
            gate.Release();
            await ObserveCompositionCompletionAsync(composition);
        }
    }

    [Fact]
    public async Task AddMonica_WhenMultipleWorkItemsFail_ShouldDrainAndReportInModuleAndScheduleOrder()
    {
        using var gate = new WorkGate(expectedEntrants: 2);
        var builder = Host.CreateApplicationBuilder();
        var composition = Task.Run(
            () => builder.AddMonica(monica =>
            {
                monica.ConfigureModuleSystem(options =>
                {
                    options.MaxConcurrentStartupWorkItems = 2;
                    options.DisableOnRegistrationError = true;
                });
                monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
                AddProbeOne(monica, options =>
                {
                    options.AddConfigureServicesWork(
                        "first-module-first-failure",
                        () => gate.Run(new InvalidOperationException("first-work-failure")));
                    options.AddConfigureServicesWork(
                        "first-module-second-failure",
                        () => gate.Run(new InvalidOperationException("second-work-failure")));
                });
                AddProbeTwo(monica, options =>
                {
                    options.AddConfigureServicesWork("second-module-success", gate.Run);
                    options.AddConfigureServicesWork(
                        "second-module-failure",
                        () => gate.Run(new InvalidOperationException("third-work-failure")));
                });
            }),
            TestContext.Current.CancellationToken);

        try
        {
            await gate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            composition.IsCompleted.Should().BeFalse();

            gate.Release();
            var exception = await Assert.ThrowsAsync<ModuleRegistrationException>(async () =>
            {
                await composition.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            });

            gate.InvocationCount.Should().Be(4);
            gate.CompletedCount.Should().Be(4);
            exception.Message.Should().Contain("first-work-failure");
            exception.Message.Should().Contain("second-work-failure");
            exception.Message.Should().Contain("third-work-failure");
            IndexOf(exception.Message, "first-module-first-failure").Should()
                .BeLessThan(IndexOf(exception.Message, "first-module-second-failure"));
            IndexOf(exception.Message, "first-module-second-failure").Should()
                .BeLessThan(IndexOf(exception.Message, "second-module-failure"));
        }
        finally
        {
            gate.Release();
            await ObserveCompositionCompletionAsync(composition);
        }
    }

    [Fact]
    public async Task AddMonica_WhenEarlyBarrierWorkFails_ShouldDrainLaterWorkWithoutCrossingTheBarrier()
    {
        using var gate = new WorkGate(expectedEntrants: 2);
        var iterationReached = NewSignal();
        var builder = Host.CreateApplicationBuilder();
        var composition = Task.Run(
            () => builder.AddMonica(monica =>
            {
                monica.ConfigureModuleSystem(options => options.MaxConcurrentStartupWorkItems = 2);
                monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
                AddProbeOne(monica, options =>
                {
                    options.AddConfigureServicesWork(
                        "early-failure",
                        () => gate.Run(new InvalidOperationException("early-barrier-failure")),
                        ModuleStartupWorkBarrier.BeforeBusinessTypeIteration);
                    options.AddConfigureServicesWork("later-success", gate.Run);
                    options.BusinessTypeIterationReached = () => iterationReached.TrySetResult();
                });
            }),
            TestContext.Current.CancellationToken);

        try
        {
            await gate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            gate.Release();

            var exception = await Assert.ThrowsAsync<ModuleRegistrationException>(async () =>
            {
                await composition.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            });

            exception.Message.Should().Contain("early-barrier-failure");
            gate.InvocationCount.Should().Be(2);
            gate.CompletedCount.Should().Be(2);
            iterationReached.Task.IsCompleted.Should().BeFalse();
        }
        finally
        {
            gate.Release();
            await ObserveCompositionCompletionAsync(composition);
        }
    }

    [Fact]
    public void AddMonica_WhenAWorkNameIsDuplicated_ShouldRejectTheSecondSchedule()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options =>
            {
                options.AddConfigureServicesWork("duplicate-work", static () => { });
                options.AddConfigureServicesWork("duplicate-work", static () => { });
            });
        });

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*already scheduled startup work named 'duplicate-work'*");
    }

    [Fact]
    public void AddMonica_WhenWorkAttemptsNestedScheduling_ShouldRejectOutsideTheSynchronousCallback()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.ScheduleNestedWork = true);
        });

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*can schedule startup work only while its synchronous ConfigureBuilder, " +
                         "ConfigureServices, IterateBusinessTypes, or PostConfigureServices callback is executing*");
    }

    [Fact]
    public async Task AddMonica_WhenWorkIsScheduledDuringBusinessTypeIteration_ShouldOverlapIterationAndHoldPostConfigure()
    {
        using var gate = new WorkGate(expectedEntrants: 1);
        var iterationReached = NewSignal();
        var postConfigureReached = NewSignal();
        var builder = Host.CreateApplicationBuilder();
        var composition = Task.Run(
            () => builder.AddMonica(monica =>
            {
                monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
                monica.AddModuleSystem();
                AddProbeOne(monica, options =>
                {
                    options.AddBusinessTypeIterationWork(
                        "iteration-work",
                        gate.Run,
                        ModuleStartupWorkBarrier.BeforePostConfigureServices);
                    options.BusinessTypeIterationReached = () => iterationReached.TrySetResult();
                    options.PostConfigureReached = () => postConfigureReached.TrySetResult();
                });
            }),
            TestContext.Current.CancellationToken);

        try
        {
            await gate.ExpectedEntrantsReached.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            await iterationReached.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

            composition.IsCompleted.Should().BeFalse();
            postConfigureReached.Task.IsCompleted.Should().BeFalse();

            gate.Release();
            await postConfigureReached.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            await composition.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);

            using var host = builder.Build();
            var work = host.Services.GetRequiredService<IModuleSystemInspectionService>()
                .GetSystemPerformance()
                .Composition.StartupWorkItems.Should().ContainSingle(item => item.Name == "iteration-work").Subject;
            work.OriginPhase.Should().Be(ModulePhase.IterateBusinessTypes);
            work.Barrier.Should().Be(ModuleStartupWorkBarrier.BeforePostConfigureServices);
        }
        finally
        {
            gate.Release();
            await ObserveCompositionCompletionAsync(composition);
        }
    }

    [Fact]
    public void AddMonica_WhenIterationWorkHasCommits_ShouldCommitInRegistrationOrderBeforePostConfigure()
    {
        using var secondWorkerCompleted = new ManualResetEventSlim(initialState: false);
        var commits = new List<string>();
        var commitThreadIds = new List<int>();
        IReadOnlyList<string>? observedAtPostConfigure = null;
        var compositionThreadId = Environment.CurrentManagedThreadId;
        var builder = Host.CreateApplicationBuilder();

        builder.AddMonica(monica =>
        {
            monica.ConfigureModuleSystem(options => options.MaxConcurrentStartupWorkItems = 2);
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModuleSystem();
            AddProbeOne(monica, options =>
            {
                options.AddBusinessTypeIterationWork(
                    "first-commit",
                    () =>
                    {
                        if (!secondWorkerCompleted.Wait(HANG_GUARD))
                        {
                            throw new TimeoutException("The second worker did not complete.");
                        }
                    },
                    () =>
                    {
                        commits.Add("first");
                        commitThreadIds.Add(Environment.CurrentManagedThreadId);
                    },
                    ModuleStartupWorkBarrier.BeforePostConfigureServices);
                options.PostConfigureReached = () => observedAtPostConfigure = commits.ToArray();
            });
            AddProbeTwo(monica, options => options.AddBusinessTypeIterationWork(
                "second-commit",
                secondWorkerCompleted.Set,
                () =>
                {
                    commits.Add("second");
                    commitThreadIds.Add(Environment.CurrentManagedThreadId);
                },
                ModuleStartupWorkBarrier.BeforePostConfigureServices));
        });

        commits.Should().Equal("first", "second");
        commitThreadIds.Should().OnlyContain(threadId => threadId == compositionThreadId);
        observedAtPostConfigure.Should().Equal("first", "second");

        using var host = builder.Build();
        var performance = host.Services.GetRequiredService<IModuleSystemInspectionService>()
            .GetSystemPerformance();
        var module = performance.Modules.Single(item =>
            item.ModuleTypeName == nameof(StartupWorkProbeModuleOne));
        var iterationExecutions = module.PhaseExecutions
            .Where(static execution => execution.Phase == ModulePhase.IterateBusinessTypes)
            .OrderBy(static execution => execution.StartedOffsetMs)
            .ToArray();
        var checkpoint = performance.Composition.StartupWorkBarriers.Single(item =>
            item.Barrier == ModuleStartupWorkBarrier.BeforePostConfigureServices);

        iterationExecutions.Should().HaveCount(2);
        checkpoint.ReleasedOffsetMs.Should().BeLessThanOrEqualTo(iterationExecutions[^1].StartedOffsetMs);
    }

    [Fact]
    public void AddMonica_WhenOneDueWorkerFails_ShouldSkipEveryCommitAtThatCheckpoint()
    {
        var successfulCommitRan = false;
        var failedWorkerCommitRan = false;
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddBusinessTypeIterationWork(
                "successful-worker",
                static () => { },
                () => successfulCommitRan = true,
                ModuleStartupWorkBarrier.BeforePostConfigureServices));
            AddProbeTwo(monica, options => options.AddBusinessTypeIterationWork(
                "failed-worker",
                static () => throw new InvalidOperationException("barrier-worker-failure"),
                () => failedWorkerCommitRan = true,
                ModuleStartupWorkBarrier.BeforePostConfigureServices));
        });

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*barrier-worker-failure*");
        successfulCommitRan.Should().BeFalse();
        failedWorkerCommitRan.Should().BeFalse();
    }

    [Fact]
    public void AddMonica_WhenACommitFails_ShouldRecordTheErrorAndStopLaterCommits()
    {
        var laterCommitRan = false;
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddBusinessTypeIterationWork(
                "failed-commit",
                static () => { },
                static () => throw new InvalidOperationException("serial-commit-failure"),
                ModuleStartupWorkBarrier.BeforePostConfigureServices));
            AddProbeTwo(monica, options => options.AddBusinessTypeIterationWork(
                "later-commit",
                static () => { },
                () => laterCommitRan = true,
                ModuleStartupWorkBarrier.BeforePostConfigureServices));
        });

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*Error committing startup work 'failed-commit'*serial-commit-failure*");
        laterCommitRan.Should().BeFalse();
    }

    [Fact]
    public void AddMonica_WhenACommitAttemptsNestedScheduling_ShouldRejectTheCommit()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.ScheduleNestedWorkFromCommit = true);
        });

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*Error committing startup work 'outer-commit-work'*" +
                         "can schedule startup work only while its synchronous*callback is executing*");
    }

    [Fact]
    public void AddMonica_WhenIterationWorkTargetsThePassedBarrier_ShouldRejectTheBarrier()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddBusinessTypeIterationWork(
                "late-iteration-work",
                static () => { },
                ModuleStartupWorkBarrier.BeforeBusinessTypeIteration));
        });

        var exception = compose.Should().Throw<Exception>().Which;
        exception.ToString().Should()
            .Contain("cannot schedule startup work for BeforeBusinessTypeIteration during business-type iteration")
            .And.Contain("Use any later barrier: BeforePostConfigureServices, " +
                         "BeforeServiceRegistrationCompletion, BeforeHostLifecycle, or NoBarrier");
    }

    [Fact]
    public void AddMonica_WhenIterationWorkFails_ShouldStopAtThePostConfigureBarrier()
    {
        var postConfigureReached = false;
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options =>
            {
                options.AddBusinessTypeIterationWork(
                    "failed-iteration-work",
                    static () => throw new InvalidOperationException("iteration-work-failure"),
                    ModuleStartupWorkBarrier.BeforePostConfigureServices);
                options.PostConfigureReached = () => postConfigureReached = true;
            });
        });

        compose.Should().Throw<Exception>()
            .Which.ToString().Should().Contain("iteration-work-failure");
        postConfigureReached.Should().BeFalse();
    }

    [Fact]
    public void AddMonica_WhenCapturedCodeSchedulesFromAnotherThread_ShouldRejectTheThread()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.ScheduleFromCapturedThread = true);
        });

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*can schedule startup work only while its synchronous ConfigureBuilder, " +
                         "ConfigureServices, IterateBusinessTypes, or PostConfigureServices callback is executing*");
    }

    [Fact]
    public void AddMonica_WhenWorkIsAnAsyncStateMachineAction_ShouldRejectBeforeInvocation()
    {
        var invocationCount = 0;
        Action asyncWork = async () =>
        {
            Interlocked.Increment(ref invocationCount);
            await Task.Yield();
        };
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddConfigureServicesWork("async-void-work", asyncWork));
        });

        var exception = compose.Should().Throw<ModuleRegistrationException>().Which;
        exception.Message.ToLowerInvariant().Should()
            .Contain("async")
            .And.Contain("startup work");
        invocationCount.Should().Be(0);
    }

    [Fact]
    public void AddMonica_WhenCommitIsAnAsyncStateMachineAction_ShouldRejectBeforeWorkerInvocation()
    {
        var workerInvocationCount = 0;
        var commitInvocationCount = 0;
        Action asyncCommit = async () =>
        {
            Interlocked.Increment(ref commitInvocationCount);
            await Task.Yield();
        };
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddBusinessTypeIterationWork(
                "async-void-commit",
                () => Interlocked.Increment(ref workerInvocationCount),
                asyncCommit,
                ModuleStartupWorkBarrier.BeforePostConfigureServices));
        });

        var exception = compose.Should().Throw<Exception>().Which;
        exception.ToString().ToLowerInvariant().Should()
            .Contain("async")
            .And.Contain("startup work commits");
        workerInvocationCount.Should().Be(0);
        commitInvocationCount.Should().Be(0);
    }

    [Fact]
    public void AddMonica_WhenPostConfigureSchedulesWorkForAPassedBarrier_ShouldRejectTheBarrier()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddPostConfigureWork(
                "late-early-work",
                static () => { },
                ModuleStartupWorkBarrier.BeforePostConfigureServices));
        });

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*startup work barrier BeforePostConfigureServices has already passed*");
    }

    [Fact]
    public void AddMonica_WhenConcurrencyLimitIsInvalid_ShouldRejectBeforeInvokingWork()
    {
        var invocationCount = 0;
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureModuleSystem(options => options.MaxConcurrentStartupWorkItems = 0);
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddConfigureServicesWork(
                "should-not-run",
                () => Interlocked.Increment(ref invocationCount)));
        });

        compose.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("MaxConcurrentStartupWorkItems");
        invocationCount.Should().Be(0);
    }

    private static IHostApplicationBuilder CreateBuilder(bool useWebHost)
    {
        if (!useWebHost)
        {
            return Host.CreateApplicationBuilder();
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        return builder;
    }

    private static IHost BuildHost(IHostApplicationBuilder builder, bool useWebHost)
    {
        if (!useWebHost)
        {
            return ((HostApplicationBuilder)builder).Build();
        }

        var app = ((WebApplicationBuilder)builder).Build();
        app.UseMonica();
        app.MapMonica();
        return app;
    }

    private static async Task ExerciseHostBoundaryAsync(IHostApplicationBuilder builder, bool useWebHost)
    {
        if (useWebHost)
        {
            await using var app = ((WebApplicationBuilder)builder).Build();
            app.UseMonica();
            app.MapMonica();
            await app.StartAsync(TestContext.Current.CancellationToken);
            await app.StopAsync(TestContext.Current.CancellationToken);
            return;
        }

        using var host = ((HostApplicationBuilder)builder).Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task ObserveCompositionCompletionAsync(Task composition)
    {
        try
        {
            await composition.WaitAsync(HANG_GUARD);
        }
        catch (Exception)
        {
            // The assertion path owns expected composition errors; cleanup only waits for workers to exit.
        }
    }

    private static TaskCompletionSource NewSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static async Task<ModuleStartupWorkPerformanceInfo> WaitForStartupWorkStatusAsync(
        global::Monica.Core.MonicaApplication application,
        string name,
        ModuleStartupWorkStatus status)
    {
        var started = Stopwatch.StartNew();
        while (started.Elapsed < HANG_GUARD)
        {
            var work = application.Profiling.GetCompositionPerformance().StartupWorkItems
                .Single(item => item.Name == name);
            if (work.Status == status)
            {
                return work;
            }

            await Task.Yield();
        }

        throw new TimeoutException($"Startup work '{name}' did not reach {status} within the test guard.");
    }

    private static int IndexOf(string value, string expected)
    {
        var index = value.IndexOf(expected, StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0);
        return index;
    }

    private static void AddProbeOne(
        IMonicaBuilder builder,
        Action<StartupWorkProbeModuleOneOption> configure)
    {
        builder.AddModule<
            StartupWorkProbeModuleOne,
            StartupWorkProbeModuleOneOption,
            StartupWorkProbeModuleOneGuide>(configure);
    }

    private static void AddProbeTwo(
        IMonicaBuilder builder,
        Action<StartupWorkProbeModuleTwoOption> configure)
    {
        builder.AddModule<
            StartupWorkProbeModuleTwo,
            StartupWorkProbeModuleTwoOption,
            StartupWorkProbeModuleTwoGuide>(configure);
    }

    private sealed class WorkGate(int expectedEntrants) : IDisposable
    {
        private readonly ManualResetEventSlim _release = new(initialState: false);
        private readonly TaskCompletionSource _expectedEntrantsReached = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeCount;
        private int _completedCount;
        private int _invocationCount;
        private int _maximumConcurrency;

        public Task ExpectedEntrantsReached => _expectedEntrantsReached.Task;

        public int CompletedCount => Volatile.Read(ref _completedCount);

        public int InvocationCount => Volatile.Read(ref _invocationCount);

        public int MaximumConcurrency => Volatile.Read(ref _maximumConcurrency);

        public void Run()
        {
            Run(null);
        }

        public void Run(Exception? failure)
        {
            var active = Interlocked.Increment(ref _activeCount);
            UpdateMaximumConcurrency(active);
            var invocation = Interlocked.Increment(ref _invocationCount);
            if (invocation >= expectedEntrants)
            {
                _expectedEntrantsReached.TrySetResult();
            }

            try
            {
                if (!_release.Wait(HANG_GUARD))
                {
                    throw new TimeoutException("The startup-work test gate was not released in time.");
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activeCount);
                Interlocked.Increment(ref _completedCount);
            }

            if (failure is not null)
            {
                throw failure;
            }
        }

        public void Release()
        {
            _release.Set();
        }

        public void Dispose()
        {
            _release.Set();
            _release.Dispose();
        }

        private void UpdateMaximumConcurrency(int candidate)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maximumConcurrency);
                if (candidate <= current
                    || Interlocked.CompareExchange(ref _maximumConcurrency, candidate, current) == current)
                {
                    return;
                }
            }
        }
    }

    private sealed class StartupTrackingLifecycleService : IHostedLifecycleService
    {
        private int _startCount;
        private int _startingCount;

        internal int StartingCount => Volatile.Read(ref _startingCount);

        internal int StartCount => Volatile.Read(ref _startCount);

        public Task StartingAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _startingCount);
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _startCount);
            return Task.CompletedTask;
        }

        public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

internal sealed record StartupWorkTestPlan(
    string Name,
    Action Work,
    ModuleStartupWorkBarrier Barrier,
    Action? Commit = null);

internal abstract class StartupWorkProbeOption<TModule> : ModuleOptions<TModule>
    where TModule : IModule
{
    internal List<StartupWorkTestPlan> ConfigureBuilderWork { get; } = [];

    internal List<StartupWorkTestPlan> ConfigureServicesWork { get; } = [];

    internal List<StartupWorkTestPlan> BusinessTypeIterationWork { get; } = [];

    internal List<StartupWorkTestPlan> PostConfigureWork { get; } = [];

    internal Action? BusinessTypeIterationReached { get; set; }

    internal Action? PostConfigureReached { get; set; }

    internal bool ScheduleNestedWork { get; set; }

    internal bool ScheduleNestedWorkFromCommit { get; set; }

    internal bool ScheduleFromCapturedThread { get; set; }

    internal void AddConfigureBuilderWork(
        string name,
        Action work,
        ModuleStartupWorkBarrier barrier =
            ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion)
    {
        ConfigureBuilderWork.Add(new StartupWorkTestPlan(name, work, barrier));
    }

    internal void AddConfigureServicesWork(
        string name,
        Action work,
        ModuleStartupWorkBarrier barrier =
            ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion)
    {
        ConfigureServicesWork.Add(new StartupWorkTestPlan(name, work, barrier));
    }

    internal void AddPostConfigureWork(
        string name,
        Action work,
        ModuleStartupWorkBarrier barrier =
            ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion)
    {
        PostConfigureWork.Add(new StartupWorkTestPlan(name, work, barrier));
    }

    internal void AddBusinessTypeIterationWork(
        string name,
        Action work,
        ModuleStartupWorkBarrier barrier =
            ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion)
    {
        BusinessTypeIterationWork.Add(new StartupWorkTestPlan(name, work, barrier));
    }

    internal void AddBusinessTypeIterationWork(
        string name,
        Action work,
        Action commit,
        ModuleStartupWorkBarrier barrier =
            ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion)
    {
        BusinessTypeIterationWork.Add(new StartupWorkTestPlan(name, work, barrier, commit));
    }
}

internal abstract class StartupWorkProbeModule<TModule, TOption, TGuide>(TOption option)
    : ModuleBase<TModule, TOption, TGuide>(option), IBusinessTypeIterator
    where TModule : ModuleBase<TModule, TOption, TGuide>
    where TOption : StartupWorkProbeOption<TModule>, new()
    where TGuide : ModuleGuide<TModule, TOption, TGuide>, new()
{
    public override void ConfigureBuilder(IHostApplicationBuilder builder)
    {
        Schedule(Option.ConfigureBuilderWork);
    }

    public override void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        Schedule(Option.ConfigureServicesWork);
        if (Option.ScheduleNestedWork)
        {
            ScheduleStartupWork(
                "outer-work",
                () => ScheduleStartupWork("nested-work", static () => { }));
        }

        if (Option.ScheduleNestedWorkFromCommit)
        {
            ScheduleStartupWork(
                "outer-commit-work",
                static () => { },
                () => ScheduleStartupWork("nested-commit-work", static () => { }),
                ModuleStartupWorkBarrier.BeforeBusinessTypeIteration);
        }

        if (Option.ScheduleFromCapturedThread)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    ScheduleStartupWork("captured-work", static () => { });
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.Start();
            thread.Join();
            if (failure is not null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        Schedule(Option.BusinessTypeIterationWork);

        Option.BusinessTypeIterationReached?.Invoke();
        foreach (var type in types)
        {
            yield return type;
        }
    }

    public override void PostConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        Option.PostConfigureReached?.Invoke();
        Schedule(Option.PostConfigureWork);
    }

    private void Schedule(IEnumerable<StartupWorkTestPlan> plans)
    {
        foreach (var plan in plans)
        {
            if (plan.Commit is null)
            {
                ScheduleStartupWork(plan.Name, plan.Work, plan.Barrier);
                continue;
            }

            ScheduleStartupWork(plan.Name, plan.Work, plan.Commit, plan.Barrier);
        }
    }
}

[ModuleKey("Test.Monica.Core.StartupWork.One")]
internal sealed class StartupWorkProbeModuleOne(StartupWorkProbeModuleOneOption option)
    : StartupWorkProbeModule<
        StartupWorkProbeModuleOne,
        StartupWorkProbeModuleOneOption,
        StartupWorkProbeModuleOneGuide>(option);

internal sealed class StartupWorkProbeModuleOneOption
    : StartupWorkProbeOption<StartupWorkProbeModuleOne>;

internal sealed class StartupWorkProbeModuleOneGuide
    : ModuleGuide<
        StartupWorkProbeModuleOne,
        StartupWorkProbeModuleOneOption,
        StartupWorkProbeModuleOneGuide>;

[ModuleKey("Test.Monica.Core.StartupWork.Two")]
internal sealed class StartupWorkProbeModuleTwo(StartupWorkProbeModuleTwoOption option)
    : StartupWorkProbeModule<
        StartupWorkProbeModuleTwo,
        StartupWorkProbeModuleTwoOption,
        StartupWorkProbeModuleTwoGuide>(option);

internal sealed class StartupWorkProbeModuleTwoOption
    : StartupWorkProbeOption<StartupWorkProbeModuleTwo>;

internal sealed class StartupWorkProbeModuleTwoGuide
    : ModuleGuide<
        StartupWorkProbeModuleTwo,
        StartupWorkProbeModuleTwoOption,
        StartupWorkProbeModuleTwoGuide>;
