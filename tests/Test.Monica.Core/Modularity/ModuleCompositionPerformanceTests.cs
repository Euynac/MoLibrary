using AwesomeAssertions;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleCompositionPerformanceTests
{
    [Fact]
    public void Initialization_WhenWebCompositionContainsHostGaps_ShouldPartitionElapsedTimeExactly()
    {
        var performance = new ModuleCompositionPerformance
        {
            ElapsedDurationMs = 13_917,
            Milestones =
            [
                Milestone(0, ModuleCompositionMilestone.CompositionStarted, 0),
                Milestone(1, ModuleCompositionMilestone.ServiceRegistrationCompleted, 8_968),
                Milestone(2, ModuleCompositionMilestone.ApplicationPipelineStarted, 10_000),
                Milestone(3, ModuleCompositionMilestone.ApplicationPipelineCompleted, 10_500),
                Milestone(4, ModuleCompositionMilestone.EndpointMappingStarted, 13_000),
                Milestone(5, ModuleCompositionMilestone.CompositionCompleted, 13_917)
            ]
        };

        var initialization = performance.Initialization;

        initialization.TotalDurationMs.Should().Be(13_917);
        initialization.MonicaFrameworkDurationMs.Should().Be(10_385);
        initialization.ApplicationConfigurationDurationMs.Should().Be(0);
        initialization.HostOwnedDurationMs.Should().Be(3_532);
        (initialization.MonicaFrameworkDurationMs
         + initialization.ApplicationConfigurationDurationMs
         + initialization.HostOwnedDurationMs)
            .Should().Be(initialization.TotalDurationMs);
    }

    [Fact]
    public void Initialization_WhenGenericHostCompletesAtRegistration_ShouldAttributeEverythingToMonica()
    {
        var performance = new ModuleCompositionPerformance
        {
            ElapsedDurationMs = 130,
            Milestones =
            [
                Milestone(0, ModuleCompositionMilestone.CompositionStarted, 0),
                Milestone(1, ModuleCompositionMilestone.ServiceRegistrationCompleted, 125),
                Milestone(2, ModuleCompositionMilestone.CompositionCompleted, 129)
            ]
        };

        performance.Initialization.Should().BeEquivalentTo(new ModuleCompositionInitializationPerformance
        {
            TotalDurationMs = 129,
            MonicaFrameworkDurationMs = 129,
            ApplicationConfigurationDurationMs = 0,
            HostOwnedDurationMs = 0
        });
    }

    [Fact]
    public void Initialization_WhenWebLifecycleIsIncomplete_ShouldCloseTheActiveMonicaIntervalAtSnapshotTime()
    {
        var performance = new ModuleCompositionPerformance
        {
            ElapsedDurationMs = 200,
            Milestones =
            [
                Milestone(0, ModuleCompositionMilestone.CompositionStarted, 0),
                Milestone(1, ModuleCompositionMilestone.ServiceRegistrationCompleted, 100),
                Milestone(2, ModuleCompositionMilestone.ApplicationPipelineStarted, 150)
            ]
        };

        performance.Initialization.Should().BeEquivalentTo(new ModuleCompositionInitializationPerformance
        {
            TotalDurationMs = 200,
            MonicaFrameworkDurationMs = 150,
            ApplicationConfigurationDurationMs = 0,
            HostOwnedDurationMs = 50
        });
    }

    [Fact]
    public void ServiceRegistration_ShouldPartitionCallbacksBlockingWaitsAndOrchestrationExactly()
    {
        var performance = new ModuleCompositionPerformance
        {
            ElapsedDurationMs = 8_968,
            Milestones =
            [
                Milestone(0, ModuleCompositionMilestone.CompositionStarted, 0),
                Milestone(1, ModuleCompositionMilestone.ServiceRegistrationCompleted, 8_968),
                Milestone(2, ModuleCompositionMilestone.CompositionCompleted, 8_968)
            ],
            ModulePhaseExecutions =
            [
                ModuleCallback(0, 100, 2_000),
                ModuleCallback(1, 2_500, 3_525)
            ],
            StartupWorkBarriers = [BlockingBarrier(2, 4_000, 8_532)]
        };

        var serviceRegistration = performance.ServiceRegistration;

        serviceRegistration.TotalDurationMs.Should().Be(8_968);
        serviceRegistration.ApplicationConfigurationDurationMs.Should().Be(0);
        serviceRegistration.SerialModuleCallbackDurationMs.Should().Be(2_925);
        serviceRegistration.BlockingWaitDurationMs.Should().Be(4_532);
        serviceRegistration.OrchestrationDurationMs.Should().Be(1_511);
        (serviceRegistration.ApplicationConfigurationDurationMs
         + serviceRegistration.SerialModuleCallbackDurationMs
         + serviceRegistration.BlockingWaitDurationMs
         + serviceRegistration.OrchestrationDurationMs)
            .Should().Be(serviceRegistration.TotalDurationMs);
    }

    [Fact]
    public void ServiceRegistration_WhenCallbackAndWaitOverlap_ShouldGiveBlockingWaitPrecedence()
    {
        var performance = new ModuleCompositionPerformance
        {
            ElapsedDurationMs = 6_000,
            Milestones =
            [
                Milestone(0, ModuleCompositionMilestone.CompositionStarted, 0),
                Milestone(1, ModuleCompositionMilestone.ServiceRegistrationCompleted, 6_000)
            ],
            ModulePhaseExecutions =
            [
                ModuleCallback(0, 1_000, 4_000),
                ModuleCallback(1, 3_500, 4_500)
            ],
            StartupWorkBarriers =
            [
                BlockingBarrier(2, 3_000, 5_000),
                BlockingBarrier(3, 4_500, 5_500)
            ]
        };

        performance.ServiceRegistration.Should().BeEquivalentTo(new ModuleServiceRegistrationPerformance
        {
            TotalDurationMs = 6_000,
            ApplicationConfigurationDurationMs = 0,
            SerialModuleCallbackDurationMs = 2_000,
            BlockingWaitDurationMs = 2_500,
            OrchestrationDurationMs = 1_500
        });
    }

    [Fact]
    public void ServiceRegistration_WhenRegistrationFailsBeforeMilestone_ShouldUseTerminalElapsedTime()
    {
        var performance = new ModuleCompositionPerformance
        {
            ElapsedDurationMs = 250,
            Milestones = [Milestone(0, ModuleCompositionMilestone.CompositionStarted, 0)],
            ModulePhaseExecutions = [ModuleCallback(0, -10, 100)]
        };

        performance.ServiceRegistration.Should().BeEquivalentTo(new ModuleServiceRegistrationPerformance
        {
            TotalDurationMs = 250,
            ApplicationConfigurationDurationMs = 0,
            SerialModuleCallbackDurationMs = 100,
            BlockingWaitDurationMs = 0,
            OrchestrationDurationMs = 150
        });
        performance.ServiceRegistrationDurationMs.Should().Be(250);
        performance.Initialization.HostOwnedDurationMs.Should().Be(0);
    }

    [Fact]
    public void ServiceRegistration_WhenApplicationConfigurationIsRecorded_ShouldNotAttributeItToMonica()
    {
        var performance = new ModuleCompositionPerformance
        {
            ElapsedDurationMs = 100,
            Milestones =
            [
                Milestone(0, ModuleCompositionMilestone.CompositionStarted, 0),
                Milestone(3, ModuleCompositionMilestone.ServiceRegistrationCompleted, 100),
                Milestone(4, ModuleCompositionMilestone.CompositionCompleted, 100)
            ],
            SystemPhases = [SystemPhase(1, "ApplicationConfiguration", 10, 35)],
            ModulePhaseExecutions = [ModuleCallback(2, 50, 80)]
        };

        performance.ServiceRegistration.Should().BeEquivalentTo(new ModuleServiceRegistrationPerformance
        {
            TotalDurationMs = 100,
            ApplicationConfigurationDurationMs = 25,
            SerialModuleCallbackDurationMs = 30,
            BlockingWaitDurationMs = 0,
            OrchestrationDurationMs = 45
        });
        performance.Initialization.Should().BeEquivalentTo(new ModuleCompositionInitializationPerformance
        {
            TotalDurationMs = 100,
            MonicaFrameworkDurationMs = 75,
            ApplicationConfigurationDurationMs = 25,
            HostOwnedDurationMs = 0
        });
    }

    [Fact]
    public void ParallelWorkActiveSpan_WhenWorkIsRunning_ShouldEndAtTheObservationBoundaryAndIgnoreQueuedWork()
    {
        var performance = new ModuleCompositionPerformance
        {
            ObservedDurationMs = 250,
            StartupWorkItems =
            [
                new ModuleStartupWorkPerformanceInfo
                {
                    Name = "completed-work",
                    StartedOffsetMs = 20,
                    CompletedOffsetMs = 80,
                    Status = ModuleStartupWorkStatus.Succeeded
                },
                new ModuleStartupWorkPerformanceInfo
                {
                    Name = "running-work",
                    StartedOffsetMs = 100,
                    Status = ModuleStartupWorkStatus.Running
                },
                new ModuleStartupWorkPerformanceInfo
                {
                    Name = "queued-work",
                    SubmittedOffsetMs = 5,
                    Status = ModuleStartupWorkStatus.Queued
                }
            ]
        };

        performance.ParallelWorkActiveSpanMs.Should().Be(230);
    }

    private static ModuleCompositionMilestonePerformanceInfo Milestone(
        long sequence,
        ModuleCompositionMilestone milestone,
        double offsetMs)
    {
        return new ModuleCompositionMilestonePerformanceInfo
        {
            Sequence = sequence,
            Milestone = milestone,
            OffsetMs = offsetMs
        };
    }

    private static ModulePhaseExecutionPerformanceInfo ModuleCallback(
        long sequence,
        double startedOffsetMs,
        double completedOffsetMs)
    {
        return new ModulePhaseExecutionPerformanceInfo
        {
            ExecutionId = $"module-phase-{sequence:D6}",
            Sequence = sequence,
            Phase = ModulePhase.ConfigureServices,
            StartedOffsetMs = startedOffsetMs,
            CompletedOffsetMs = completedOffsetMs
        };
    }

    private static ModuleSystemPhasePerformanceInfo SystemPhase(
        long sequence,
        string phaseName,
        double startedOffsetMs,
        double completedOffsetMs)
    {
        return new ModuleSystemPhasePerformanceInfo
        {
            Sequence = sequence,
            PhaseName = phaseName,
            StartedOffsetMs = startedOffsetMs,
            CompletedOffsetMs = completedOffsetMs
        };
    }

    private static ModuleStartupWorkBarrierPerformanceInfo BlockingBarrier(
        long sequence,
        double enteredOffsetMs,
        double releasedOffsetMs)
    {
        return new ModuleStartupWorkBarrierPerformanceInfo
        {
            Sequence = sequence,
            Barrier = ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion,
            EnteredOffsetMs = enteredOffsetMs,
            ReleasedOffsetMs = releasedOffsetMs,
            PendingWorkItems =
            [
                new ModuleStartupWorkBarrierPendingWorkInfo
                {
                    WorkItemId = $"work-{sequence}",
                    RemainingDurationMs = Math.Max(0, releasedOffsetMs - enteredOffsetMs)
                }
            ]
        };
    }
}
