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
        var mapsterWork = new ModuleCompositionWorkPerformanceInfo
        {
            WorkItemId = "ObjectMapping:0:compile-mapster-configuration",
            Sequence = 0,
            ModuleKey = objectMappingKey,
            ModuleTypeName = "ModuleObjectMapping",
            ModuleFullTypeName = "Monica.Core.Modules.ModuleObjectMapping",
            ModuleRegistrationOrder = 17,
            Name = "compile-mapster-configuration",
            OriginPhase = ModulePhase.PostConfigureServices,
            Deadline = ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion,
            Status = ModuleCompositionWorkStatus.Succeeded,
            SubmittedOffsetMs = 4_000,
            StartedOffsetMs = 4_002,
            CompletedOffsetMs = 13_714,
            WasPendingAtDeadline = true,
            RemainingAtDeadlineMs = 9_668,
            IsDeadlineReleaser = true
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
                    SystemPhase(0, "ServiceRegistration", 0, 13_714),
                    SystemPhase(1, "ConfigureApplicationBuilder", 13_750, 100),
                    SystemPhase(2, "ConfigureEndpoints", 13_900, 17)
                ],
                ModulePhaseExecutions = [configurationCallback, objectMappingCallback, remainingCallback],
                WorkItems = [mapsterWork],
                Checkpoints =
                [
                    new ModuleCompositionCheckpointPerformanceInfo
                    {
                        Sequence = 0,
                        Deadline = ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion,
                        EnteredOffsetMs = 4_046,
                        ReleasedOffsetMs = 13_714,
                        DueWorkItemIds = [mapsterWork.WorkItemId],
                        PendingWorkItems =
                        [
                            new ModuleCompositionCheckpointPendingWorkInfo
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

        dashboard.Summary.EndToEndElapsedMs.Should().Be(13_917);
        dashboard.Summary.AggregateSerialCallbackMs.Should().Be(1_561);
        dashboard.Summary.ParallelWorkActiveSpanMs.Should().Be(9_712);
        dashboard.Summary.AggregateWorkerExecutionMs.Should().Be(9_712);
        dashboard.Summary.AggregateWorkerQueueMs.Should().Be(2);
        dashboard.Summary.StartupBlockingWaitMs.Should().Be(9_668);

        dashboard.CriticalPath.Should().BeEquivalentTo(new ModuleCriticalPathView(
            ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion.ToString(),
            "ModuleObjectMapping",
            objectMappingKey,
            "compile-mapster-configuration",
            9_668));
        dashboard.WorkItems.Should().ContainSingle(item =>
            item.ModuleTypeName == "ModuleObjectMapping"
            && item.Name == "compile-mapster-configuration"
            && item.ExecutionDurationMs == 9_712
            && item.WasPendingAtDeadline);
        dashboard.SerialModules.Should().ContainSingle(module =>
            module.ModuleTypeName == "ModuleObjectMapping"
            && module.AggregateDurationMs == 85
            && module.WorkItemCount == 1);
        dashboard.Checkpoints.Should().ContainSingle(checkpoint =>
            checkpoint.WaitDurationMs == 9_668
            && checkpoint.DueWorkItemNames.Contains("ModuleObjectMapping / compile-mapster-configuration")
            && checkpoint.PendingWorkItems.Single().RemainingDurationMs == 9_668
            && checkpoint.ReleasingWorkItemName == "ModuleObjectMapping / compile-mapster-configuration");
        dashboard.Milestones.Should().Contain(milestone =>
            milestone.Name == ModuleCompositionMilestone.ApplicationPipelineStarted.ToString()
            && milestone.ElapsedSincePreviousMs == 36
            && milestone.IsHostOwnedGap);
        dashboard.Milestones.Should().Contain(milestone =>
            milestone.Name == ModuleCompositionMilestone.EndpointMappingStarted.ToString()
            && milestone.ElapsedSincePreviousMs == 50
            && milestone.IsHostOwnedGap);
        dashboard.Milestones.Should().Contain(milestone =>
            milestone.Name == ModuleCompositionMilestone.CompositionCompleted.ToString()
            && milestone.ElapsedSincePreviousMs == 17
            && !milestone.IsHostOwnedGap);
        dashboard.Milestones.Select(static milestone => milestone.Sequence).Should().Equal(1, 2, 3, 4, 5, 6);
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

        dashboard.Summary.SerialCallbackCount.Should().Be(2);
        dashboard.Summary.AggregateSerialCallbackMs.Should().Be(55);
        dashboard.Summary.WorkItemCount.Should().Be(0);
        dashboard.Summary.ParallelWorkActiveSpanMs.Should().Be(0);
        dashboard.CriticalPath.Should().BeNull();
        dashboard.SerialModules.Should().ContainSingle(module =>
            module.CallbackCount == 2
            && module.SlowestPhase == ModulePhase.ConfigureServices.ToString()
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
            status: ModuleCompositionWorkStatus.Succeeded);
        var failed = Work(
            sequence: 1,
            name: "failed-work",
            startedOffsetMs: 20,
            completedOffsetMs: 70,
            status: ModuleCompositionWorkStatus.Failed,
            errorMessage: "mapping failure");
        var performance = new ModuleSystemPerformance
        {
            Composition = new ModuleCompositionPerformance
            {
                ElapsedDurationMs = 120,
                WorkItems = [first, failed],
                Checkpoints =
                [
                    new ModuleCompositionCheckpointPerformanceInfo
                    {
                        Deadline = ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion,
                        EnteredOffsetMs = 30,
                        ReleasedOffsetMs = 110,
                        DueWorkItemIds = [first.WorkItemId, failed.WorkItemId],
                        PendingWorkItems =
                        [
                            new ModuleCompositionCheckpointPendingWorkInfo
                            {
                                WorkItemId = first.WorkItemId,
                                RemainingDurationMs = 80
                            },
                            new ModuleCompositionCheckpointPendingWorkInfo
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

        dashboard.WorkItems.Should().HaveCount(2);
        dashboard.WorkItems.Should().Contain(item =>
            item.Name == "failed-work"
            && item.Status == ModuleCompositionWorkStatus.Failed.ToString()
            && item.ErrorMessage == "mapping failure");
        dashboard.Summary.ParallelWorkActiveSpanMs.Should().Be(100);
        dashboard.Summary.AggregateWorkerExecutionMs.Should().Be(150);
        dashboard.Summary.StartupBlockingWaitMs.Should().Be(80);
        dashboard.CriticalPath?.ReleasingWorkName.Should().Be("first-work");
        dashboard.Checkpoints.Single().PendingWorkItems.Should().HaveCount(2);
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
        params ModuleCompositionWorkPerformanceInfo[] workItems) => new()
    {
        ModuleKey = callback.ModuleKey,
        ModuleTypeName = callback.ModuleTypeName,
        RegistrationOrder = callback.ModuleRegistrationOrder,
        IsRuntimeAvailable = true,
        PhaseExecutions = [callback],
        CompositionWorkItems = workItems
    };

    private static ModuleCompositionWorkPerformanceInfo Work(
        long sequence,
        string name,
        double startedOffsetMs,
        double completedOffsetMs,
        ModuleCompositionWorkStatus status,
        string? errorMessage = null) => new()
    {
        WorkItemId = $"work-{sequence}",
        Sequence = sequence,
        ModuleKey = (ModuleKey)BuiltInModuleKey.ObjectMapping,
        ModuleTypeName = "ModuleObjectMapping",
        ModuleRegistrationOrder = 17,
        Name = name,
        OriginPhase = ModulePhase.PostConfigureServices,
        Deadline = ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion,
        Status = status,
        SubmittedOffsetMs = startedOffsetMs,
        StartedOffsetMs = startedOffsetMs,
        CompletedOffsetMs = completedOffsetMs,
        WasPendingAtDeadline = true,
        RemainingAtDeadlineMs = completedOffsetMs - 30,
        ErrorMessage = errorMessage
    };
}
