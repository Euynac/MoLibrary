using AwesomeAssertions;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;
using Monica.UI.UIModuleSystem.Support;
using Xunit;

namespace Test.Monica.UI.UIModuleSystem;

public sealed class ModulePerformanceDashboardViewFactoryTests
{
    [Fact]
    public void Create_exposes_the_blocking_Mapster_work_separately_from_its_serial_module_callback()
    {
        var objectMappingKey = (ModuleKey)BuiltInModuleKey.ObjectMapping;
        var configurationKey = (ModuleKey)BuiltInModuleKey.Configuration;
        var mapsterWork = new ModuleStartupWorkPerformanceInfo
        {
            WorkItemId = "ObjectMapping:0:compile-mapster-configuration",
            Sequence = 0,
            ModuleKey = objectMappingKey,
            ModuleTypeName = "ModuleObjectMapping",
            ModuleFullTypeName = "Monica.Core.Modules.ModuleObjectMapping",
            ModuleRegistrationOrder = 17,
            Name = "compile-mapster-configuration",
            OriginPhase = ModulePhase.PostConfigureServices,
            Barrier = ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion,
            Status = ModuleStartupWorkStatus.Succeeded,
            SubmittedOffsetMs = 4_000,
            StartedOffsetMs = 4_002,
            CompletedOffsetMs = 13_714,
            WasPendingAtBarrier = true,
            RemainingAtBarrierMs = 9_668,
            IsBarrierReleaser = true,
            QueueDurationMs = 2,
            ExecutionDurationMs = 9_712
        };
        var objectMappingCallback = Callback(
            sequence: 1,
            objectMappingKey,
            "ModuleObjectMapping",
            order: 17,
            ModulePhase.PostConfigureServices,
            startedOffsetMs: 3_800,
            durationMs: 85);
        var configurationCallback = Callback(
            sequence: 0,
            configurationKey,
            "ModuleConfiguration",
            order: 3,
            ModulePhase.ConfigureServices,
            startedOffsetMs: 900,
            durationMs: 934);
        var remainingCallback = Callback(
            sequence: 2,
            (ModuleKey)BuiltInModuleKey.Logging,
            "ModuleLogging",
            order: 4,
            ModulePhase.ConfigureServices,
            startedOffsetMs: 1_900,
            durationMs: 542);
        var performance = new ModuleSystemPerformance
        {
            Composition = new ModuleCompositionPerformance
            {
                ElapsedDurationMs = 13_917,
                Milestones =
                [
                    Milestone(0, ModuleCompositionMilestone.CompositionStarted, 0),
                    Milestone(40, ModuleCompositionMilestone.ServiceRegistrationCompleted, 13_714),
                    Milestone(41, ModuleCompositionMilestone.ApplicationPipelineStarted, 13_750),
                    Milestone(50, ModuleCompositionMilestone.ApplicationPipelineCompleted, 13_850),
                    Milestone(51, ModuleCompositionMilestone.EndpointMappingStarted, 13_900),
                    Milestone(60, ModuleCompositionMilestone.CompositionCompleted, 13_917)
                ],
                SystemPhases =
                [
                    SystemPhase(0, "ApplicationConfiguration", 100, 500),
                    SystemPhase(1, "ServiceRegistration", 0, 13_714),
                    SystemPhase(2, "ConfigureApplicationBuilder", 13_750, 100),
                    SystemPhase(3, "ConfigureEndpoints", 13_900, 17)
                ],
                ModulePhaseExecutions = [configurationCallback, objectMappingCallback, remainingCallback],
                StartupWorkItems = [mapsterWork],
                StartupWorkBarriers =
                [
                    new ModuleStartupWorkBarrierPerformanceInfo
                    {
                        Sequence = 0,
                        Barrier = ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion,
                        EnteredOffsetMs = 4_046,
                        ReleasedOffsetMs = 13_714,
                        DueWorkItemIds = [mapsterWork.WorkItemId],
                        PendingWorkItems =
                        [
                            new ModuleStartupWorkBarrierPendingWorkInfo
                            {
                                WorkItemId = mapsterWork.WorkItemId,
                                RemainingDurationMs = 9_668
                            }
                        ],
                        ReleasingWorkItemId = mapsterWork.WorkItemId
                    }
                ]
            },
            Modules =
            [
                Module(configurationCallback),
                Module(objectMappingCallback, mapsterWork),
                Module(remainingCallback)
            ]
        };

        var dashboard = ModulePerformanceDashboardViewFactory.Create(performance);

        dashboard.Summary.Initialization.Should().BeEquivalentTo(new ModuleInitializationBreakdownView(
            TotalDurationMs: 13_917,
            MonicaFrameworkDurationMs: 13_331,
            ApplicationConfigurationDurationMs: 500,
            HostOwnedDurationMs: 86));
        dashboard.Summary.ServiceRegistration.Should().BeEquivalentTo(new ModuleServiceRegistrationBreakdownView(
            TotalDurationMs: 13_714,
            ApplicationConfigurationDurationMs: 500,
            SerialModuleCallbackDurationMs: 1_561,
            BlockingWaitDurationMs: 9_668,
            OrchestrationDurationMs: 1_985));
        (dashboard.Summary.Initialization.MonicaFrameworkDurationMs
         + dashboard.Summary.Initialization.ApplicationConfigurationDurationMs
         + dashboard.Summary.Initialization.HostOwnedDurationMs)
            .Should().Be(dashboard.Summary.Initialization.TotalDurationMs);
        (dashboard.Summary.ServiceRegistration.ApplicationConfigurationDurationMs
         + dashboard.Summary.ServiceRegistration.SerialModuleCallbackDurationMs
         + dashboard.Summary.ServiceRegistration.BlockingWaitDurationMs
         + dashboard.Summary.ServiceRegistration.OrchestrationDurationMs)
            .Should().Be(dashboard.Summary.ServiceRegistration.TotalDurationMs);
        dashboard.Summary.ServiceRegistrationCallbackCount.Should().Be(3);
        dashboard.Summary.ParallelActivity.Should().BeEquivalentTo(new ModuleParallelActivityView(
            ActiveSpanMs: 9_712,
            AggregateExecutionMs: 9_712,
            AggregateQueueMs: 2));

        dashboard.CriticalPath.Should().BeEquivalentTo(new ModuleCriticalPathView(
            ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion,
            "ModuleObjectMapping",
            objectMappingKey,
            "compile-mapster-configuration",
            9_668));
        dashboard.StartupWorkItems.Should().ContainSingle(item =>
            item.ModuleTypeName == "ModuleObjectMapping"
            && item.Name == "compile-mapster-configuration"
            && item.ExecutionDurationMs == 9_712
            && item.WasPendingAtBarrier);
        dashboard.SerialModules.Should().ContainSingle(module =>
            module.ModuleTypeName == "ModuleObjectMapping"
            && module.AggregateDurationMs == 85
            && module.WorkItemCount == 1);
        dashboard.StartupWorkBarriers.Should().ContainSingle(barrier =>
            barrier.WaitDurationMs == 9_668
            && barrier.DueWorkItemNames.Contains("ModuleObjectMapping / compile-mapster-configuration")
            && barrier.PendingWorkItems.Single().RemainingDurationMs == 9_668
            && barrier.ReleasingWorkItemName == "ModuleObjectMapping / compile-mapster-configuration");
        dashboard.Milestones.Should().Contain(milestone =>
            milestone.Name == ModuleCompositionMilestone.ApplicationPipelineStarted
            && milestone.ElapsedSincePreviousMs == 36
            && milestone.IsHostOwnedGap);
        dashboard.Milestones.Should().Contain(milestone =>
            milestone.Name == ModuleCompositionMilestone.EndpointMappingStarted
            && milestone.ElapsedSincePreviousMs == 50
            && milestone.IsHostOwnedGap);
        dashboard.Milestones.Should().Contain(milestone =>
            milestone.Name == ModuleCompositionMilestone.CompositionCompleted
            && milestone.ElapsedSincePreviousMs == 17
            && !milestone.IsHostOwnedGap);
        dashboard.Milestones.Select(static milestone => milestone.Sequence).Should().Equal(1, 2, 3, 4, 5, 6);
        dashboard.SystemPhases.Select(static phase => phase.RelativeToLongestPercentage)
            .Should().Equal(
                500d / 13_714 * 100,
                100,
                100d / 13_714 * 100,
                17d / 13_714 * 100);
    }

    [Fact]
    public void Create_preserves_repeated_callbacks_and_reports_zero_parallel_work_without_false_critical_path()
    {
        var key = (ModuleKey)BuiltInModuleKey.Logging;
        var first = Callback(0, key, "ModuleLogging", 1, ModulePhase.ConfigureServices, 10, 25);
        var second = Callback(1, key, "ModuleLogging", 1, ModulePhase.ConfigureServices, 40, 30);
        var performance = new ModuleSystemPerformance
        {
            Composition = new ModuleCompositionPerformance
            {
                ElapsedDurationMs = 100,
                ModulePhaseExecutions = [first, second],
                Milestones =
                [
                    Milestone(0, ModuleCompositionMilestone.CompositionStarted, 0),
                    Milestone(1, ModuleCompositionMilestone.CompositionCompleted, 100)
                ]
            },
            Modules = [new ModulePerformanceInfo
            {
                ModuleKey = key,
                ModuleTypeName = "ModuleLogging",
                RegistrationOrder = 1,
                PhaseExecutions = [first, second]
            }]
        };

        var dashboard = ModulePerformanceDashboardViewFactory.Create(performance);

        dashboard.Summary.ServiceRegistrationCallbackCount.Should().Be(2);
        dashboard.Summary.ServiceRegistration.SerialModuleCallbackDurationMs.Should().Be(55);
        dashboard.Summary.ServiceRegistration.OrchestrationDurationMs.Should().Be(45);
        dashboard.Summary.WorkItemCount.Should().Be(0);
        dashboard.Summary.ParallelActivity.ActiveSpanMs.Should().Be(0);
        dashboard.CriticalPath.Should().BeNull();
        dashboard.SerialModules.Should().ContainSingle(module =>
            module.CallbackCount == 2
            && module.SlowestPhase == ModulePhase.ConfigureServices
            && module.SlowestPhaseDurationMs == 55);
        dashboard.Milestones.Select(static milestone => milestone.ElapsedSincePreviousMs)
            .Should().Equal(0, 100);
    }

    [Fact]
    public void Create_WhenConcurrentWorkIncludesFailure_ShouldPreserveEveryStatusAndExactBarrierReleaser()
    {
        var first = Work(
            sequence: 0,
            name: "first-work",
            startedOffsetMs: 10,
            completedOffsetMs: 110,
            status: ModuleStartupWorkStatus.Succeeded);
        var failed = Work(
            sequence: 1,
            name: "failed-work",
            startedOffsetMs: 20,
            completedOffsetMs: 70,
            status: ModuleStartupWorkStatus.Failed,
            errorMessage: "mapping failure");
        var performance = new ModuleSystemPerformance
        {
            Composition = new ModuleCompositionPerformance
            {
                ElapsedDurationMs = 120,
                StartupWorkItems = [first, failed],
                StartupWorkBarriers =
                [
                    new ModuleStartupWorkBarrierPerformanceInfo
                    {
                        Barrier = ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion,
                        EnteredOffsetMs = 30,
                        ReleasedOffsetMs = 110,
                        DueWorkItemIds = [first.WorkItemId, failed.WorkItemId],
                        PendingWorkItems =
                        [
                            new ModuleStartupWorkBarrierPendingWorkInfo
                            {
                                WorkItemId = first.WorkItemId,
                                RemainingDurationMs = 80
                            },
                            new ModuleStartupWorkBarrierPendingWorkInfo
                            {
                                WorkItemId = failed.WorkItemId,
                                RemainingDurationMs = 40
                            }
                        ],
                        ReleasingWorkItemId = first.WorkItemId
                    }
                ]
            }
        };

        var dashboard = ModulePerformanceDashboardViewFactory.Create(performance);

        dashboard.StartupWorkItems.Should().HaveCount(2);
        dashboard.StartupWorkItems.Should().Contain(item =>
            item.Name == "failed-work"
            && item.Status == ModuleStartupWorkStatus.Failed
            && item.ErrorMessage == "mapping failure");
        dashboard.Summary.ParallelActivity.ActiveSpanMs.Should().Be(100);
        dashboard.Summary.ParallelActivity.AggregateExecutionMs.Should().Be(150);
        dashboard.Summary.ServiceRegistration.BlockingWaitDurationMs.Should().Be(80);
        dashboard.Summary.ServiceRegistration.OrchestrationDurationMs.Should().Be(40);
        dashboard.CriticalPath?.ReleasingWorkName.Should().Be("first-work");
        dashboard.StartupWorkBarriers.Single().PendingWorkItems.Should().HaveCount(2);
    }

    [Fact]
    public void Create_distinguishes_live_non_blocking_work_from_live_work_awaiting_a_future_barrier()
    {
        var work = new ModuleStartupWorkPerformanceInfo
        {
            WorkItemId = "ObjectMapping:0:compile-mapster-configuration",
            Sequence = 0,
            ModuleKey = (ModuleKey)BuiltInModuleKey.ObjectMapping,
            ModuleTypeName = "ModuleObjectMapping",
            ModuleRegistrationOrder = 17,
            Name = "compile-mapster-configuration",
            OriginPhase = ModulePhase.PostConfigureServices,
            Barrier = ModuleStartupWorkBarrier.NoBarrier,
            Status = ModuleStartupWorkStatus.Running,
            SubmittedOffsetMs = 40,
            StartedOffsetMs = 42,
            QueueDurationMs = 2
        };
        var requiredWork = new ModuleStartupWorkPerformanceInfo
        {
            WorkItemId = "Configuration:1:build-configuration-definitions",
            Sequence = 1,
            ModuleKey = (ModuleKey)BuiltInModuleKey.Configuration,
            ModuleTypeName = "ModuleConfiguration",
            ModuleRegistrationOrder = 3,
            Name = "build-configuration-definitions",
            OriginPhase = ModulePhase.IterateBusinessTypes,
            Barrier = ModuleStartupWorkBarrier.BeforeHostLifecycle,
            Status = ModuleStartupWorkStatus.Queued,
            SubmittedOffsetMs = 45
        };
        var performance = new ModuleSystemPerformance
        {
            Composition = new ModuleCompositionPerformance
            {
                ElapsedDurationMs = 100,
                StartupWorkItems = [work, requiredWork]
            }
        };

        var dashboard = ModulePerformanceDashboardViewFactory.Create(performance);

        dashboard.StartupWorkItems.Should().ContainSingle(item =>
            item.Barrier == ModuleStartupWorkBarrier.NoBarrier
            && item.Status == ModuleStartupWorkStatus.Running
            && item.Impact == ModuleStartupWorkImpact.DoesNotBlockStartup
            && !item.WasPendingAtBarrier
            && !item.RemainingAtBarrierMs.HasValue);
        dashboard.StartupWorkItems.Should().ContainSingle(item =>
            item.Barrier == ModuleStartupWorkBarrier.BeforeHostLifecycle
            && item.Status == ModuleStartupWorkStatus.Queued
            && item.Impact == ModuleStartupWorkImpact.PendingBeforeBarrier
            && !item.WasPendingAtBarrier);
        dashboard.StartupWorkBarriers.Should().BeEmpty();
        dashboard.CriticalPath.Should().BeNull();
    }

    private static ModuleCompositionMilestonePerformanceInfo Milestone(
        long sequence,
        ModuleCompositionMilestone milestone,
        double offsetMs) => new()
    {
        Sequence = sequence,
        Milestone = milestone,
        OffsetMs = offsetMs
    };

    private static ModuleSystemPhasePerformanceInfo SystemPhase(
        long sequence,
        string phaseName,
        double startedOffsetMs,
        double durationMs) => new()
    {
        Sequence = sequence,
        PhaseName = phaseName,
        StartedOffsetMs = startedOffsetMs,
        CompletedOffsetMs = startedOffsetMs + durationMs
    };

    private static ModulePhaseExecutionPerformanceInfo Callback(
        long sequence,
        ModuleKey moduleKey,
        string moduleTypeName,
        int order,
        ModulePhase phase,
        double startedOffsetMs,
        double durationMs) => new()
    {
        ExecutionId = $"callback-{sequence}",
        Sequence = sequence,
        ModuleKey = moduleKey,
        ModuleTypeName = moduleTypeName,
        ModuleRegistrationOrder = order,
        Phase = phase,
        StartedOffsetMs = startedOffsetMs,
        CompletedOffsetMs = startedOffsetMs + durationMs
    };

    private static ModulePerformanceInfo Module(
        ModulePhaseExecutionPerformanceInfo callback,
        params ModuleStartupWorkPerformanceInfo[] workItems) => new()
    {
        ModuleKey = callback.ModuleKey,
        ModuleTypeName = callback.ModuleTypeName,
        RegistrationOrder = callback.ModuleRegistrationOrder,
        IsRuntimeAvailable = true,
        PhaseExecutions = [callback],
        StartupWorkItems = workItems
    };

    private static ModuleStartupWorkPerformanceInfo Work(
        long sequence,
        string name,
        double startedOffsetMs,
        double completedOffsetMs,
        ModuleStartupWorkStatus status,
        string? errorMessage = null) => new()
    {
        WorkItemId = $"work-{sequence}",
        Sequence = sequence,
        ModuleKey = (ModuleKey)BuiltInModuleKey.ObjectMapping,
        ModuleTypeName = "ModuleObjectMapping",
        ModuleRegistrationOrder = 17,
        Name = name,
        OriginPhase = ModulePhase.PostConfigureServices,
        Barrier = ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion,
        Status = status,
        SubmittedOffsetMs = startedOffsetMs,
        StartedOffsetMs = startedOffsetMs,
        CompletedOffsetMs = completedOffsetMs,
        WasPendingAtBarrier = true,
        RemainingAtBarrierMs = completedOffsetMs - 30,
        ExecutionDurationMs = completedOffsetMs - startedOffsetMs,
        ErrorMessage = errorMessage
    };
}
