using System.Runtime.ExceptionServices;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Diagnostics.Services;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleCompositionWorkTests
{
    private static readonly TimeSpan HANG_GUARD = TimeSpan.FromSeconds(10);

    [Fact]
    public void DeadlineContract_ShouldContainOnlyDeterministicCompositionBarriers()
    {
        Enum.GetNames<ModuleCompositionWorkDeadline>().Should().Equal(
            nameof(ModuleCompositionWorkDeadline.BeforeBusinessTypeIteration),
            nameof(ModuleCompositionWorkDeadline.BeforePostConfigureServices),
            nameof(ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion));
    }

    [Fact]
    public void Diagnostics_AfterCompositionWorkCompletes_ShouldKeepSerialAndWorkerDurationsSeparate()
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
        var module = performance.ModulePerformances.Single(item =>
            item.ModuleTypeName == nameof(CompositionWorkProbeModuleOne));
        var work = module.CompositionWorkItems.Should().ContainSingle().Subject;

        performance.CompositionWorkItemCount.Should().Be(1);
        performance.CompositionCheckpoints.Select(static checkpoint => checkpoint.Deadline).Should().Equal(
            ModuleCompositionWorkDeadline.BeforeBusinessTypeIteration,
            ModuleCompositionWorkDeadline.BeforePostConfigureServices,
            ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion);
        module.SerialPhaseDurationMs.Should().Be(module.PhaseDurations.Values.Sum());
        work.Name.Should().Be("diagnostic-work");
        work.OriginPhase.Should().Be(ModulePhase.ConfigureServices);
        work.Deadline.Should().Be(ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion);
        work.Status.Should().Be(ModuleCompositionWorkStatus.Succeeded);
        work.SubmittedAtUtc.Should().BeOnOrBefore(work.StartedAtUtc);
        work.StartedAtUtc.Should().BeOnOrBefore(work.CompletedAtUtc);
    }

    [Fact]
    public void AddMonica_WhenCompositionWorkersStart_ShouldNotFlowTheCallersExecutionContext()
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
    public void AddMonica_WhenExecutionContextFlowIsAlreadySuppressed_ShouldStillRunCompositionWork()
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
                        ModuleCompositionWorkDeadline.BeforeBusinessTypeIteration);
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
                        ModuleCompositionWorkDeadline.BeforePostConfigureServices);
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
                monica.ConfigureModuleSystem(options => options.MaxConcurrentCompositionWorkItems = 2);
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
                    options.MaxConcurrentCompositionWorkItems = 2;
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
    public async Task AddMonica_WhenEarlyDeadlineWorkFails_ShouldDrainLaterWorkWithoutCrossingTheCheckpoint()
    {
        using var gate = new WorkGate(expectedEntrants: 2);
        var iterationReached = NewSignal();
        var builder = Host.CreateApplicationBuilder();
        var composition = Task.Run(
            () => builder.AddMonica(monica =>
            {
                monica.ConfigureModuleSystem(options => options.MaxConcurrentCompositionWorkItems = 2);
                monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
                AddProbeOne(monica, options =>
                {
                    options.AddConfigureServicesWork(
                        "early-failure",
                        () => gate.Run(new InvalidOperationException("early-deadline-failure")),
                        ModuleCompositionWorkDeadline.BeforeBusinessTypeIteration);
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

            exception.Message.Should().Contain("early-deadline-failure");
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
            .WithMessage("*already scheduled composition work named 'duplicate-work'*");
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
            .WithMessage("*can schedule composition work only while its synchronous ConfigureBuilder, " +
                         "ConfigureServices, or PostConfigureServices callback is executing*");
    }

    [Fact]
    public void AddMonica_WhenWorkIsScheduledDuringBusinessTypeIteration_ShouldRejectOutsideAllowedCallbacks()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.ScheduleDuringBusinessTypeIteration = true);
        });

        var exception = compose.Should().Throw<Exception>().Which;
        exception.ToString().Should().Contain(
            "can schedule composition work only while its synchronous ConfigureBuilder, " +
            "ConfigureServices, or PostConfigureServices callback is executing");
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
            .WithMessage("*can schedule composition work only while its synchronous ConfigureBuilder, " +
                         "ConfigureServices, or PostConfigureServices callback is executing*");
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
            .And.Contain("composition work");
        invocationCount.Should().Be(0);
    }

    [Fact]
    public void AddMonica_WhenPostConfigureSchedulesWorkForAPassedDeadline_ShouldRejectTheDeadline()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddPostConfigureWork(
                "late-early-work",
                static () => { },
                ModuleCompositionWorkDeadline.BeforePostConfigureServices));
        });

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*deadline BeforePostConfigureServices has already passed*");
    }

    [Fact]
    public void AddMonica_WhenConcurrencyLimitIsInvalid_ShouldRejectBeforeInvokingWork()
    {
        var invocationCount = 0;
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureModuleSystem(options => options.MaxConcurrentCompositionWorkItems = 0);
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            AddProbeOne(monica, options => options.AddConfigureServicesWork(
                "should-not-run",
                () => Interlocked.Increment(ref invocationCount)));
        });

        compose.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("MaxConcurrentCompositionWorkItems");
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

    private static int IndexOf(string value, string expected)
    {
        var index = value.IndexOf(expected, StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0);
        return index;
    }

    private static void AddProbeOne(
        IMonicaBuilder builder,
        Action<CompositionWorkProbeModuleOneOption> configure)
    {
        builder.AddModule<
            CompositionWorkProbeModuleOne,
            CompositionWorkProbeModuleOneOption,
            CompositionWorkProbeModuleOneGuide>(configure);
    }

    private static void AddProbeTwo(
        IMonicaBuilder builder,
        Action<CompositionWorkProbeModuleTwoOption> configure)
    {
        builder.AddModule<
            CompositionWorkProbeModuleTwo,
            CompositionWorkProbeModuleTwoOption,
            CompositionWorkProbeModuleTwoGuide>(configure);
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
                    throw new TimeoutException("The composition-work test gate was not released in time.");
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
}

internal sealed record CompositionWorkTestPlan(
    string Name,
    Action Work,
    ModuleCompositionWorkDeadline Deadline);

internal abstract class CompositionWorkProbeOption<TModule> : ModuleOptions<TModule>
    where TModule : IModule
{
    internal List<CompositionWorkTestPlan> ConfigureBuilderWork { get; } = [];

    internal List<CompositionWorkTestPlan> ConfigureServicesWork { get; } = [];

    internal List<CompositionWorkTestPlan> PostConfigureWork { get; } = [];

    internal Action? BusinessTypeIterationReached { get; set; }

    internal Action? PostConfigureReached { get; set; }

    internal bool ScheduleDuringBusinessTypeIteration { get; set; }

    internal bool ScheduleNestedWork { get; set; }

    internal bool ScheduleFromCapturedThread { get; set; }

    internal void AddConfigureBuilderWork(
        string name,
        Action work,
        ModuleCompositionWorkDeadline deadline =
            ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion)
    {
        ConfigureBuilderWork.Add(new CompositionWorkTestPlan(name, work, deadline));
    }

    internal void AddConfigureServicesWork(
        string name,
        Action work,
        ModuleCompositionWorkDeadline deadline =
            ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion)
    {
        ConfigureServicesWork.Add(new CompositionWorkTestPlan(name, work, deadline));
    }

    internal void AddPostConfigureWork(
        string name,
        Action work,
        ModuleCompositionWorkDeadline deadline =
            ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion)
    {
        PostConfigureWork.Add(new CompositionWorkTestPlan(name, work, deadline));
    }
}

internal abstract class CompositionWorkProbeModule<TModule, TOption, TGuide>(TOption option)
    : ModuleBase<TModule, TOption, TGuide>(option), IBusinessTypeIterator
    where TModule : ModuleBase<TModule, TOption, TGuide>
    where TOption : CompositionWorkProbeOption<TModule>, new()
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
            ScheduleCompositionWork(
                "outer-work",
                () => ScheduleCompositionWork("nested-work", static () => { }));
        }

        if (Option.ScheduleFromCapturedThread)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    ScheduleCompositionWork("captured-work", static () => { });
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
        if (Option.ScheduleDuringBusinessTypeIteration)
        {
            ScheduleCompositionWork("iteration-work", static () => { });
        }

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

    private void Schedule(IEnumerable<CompositionWorkTestPlan> plans)
    {
        foreach (var plan in plans)
        {
            ScheduleCompositionWork(plan.Name, plan.Work, plan.Deadline);
        }
    }
}

[ModuleKey("Test.Monica.Core.CompositionWork.One")]
internal sealed class CompositionWorkProbeModuleOne(CompositionWorkProbeModuleOneOption option)
    : CompositionWorkProbeModule<
        CompositionWorkProbeModuleOne,
        CompositionWorkProbeModuleOneOption,
        CompositionWorkProbeModuleOneGuide>(option);

internal sealed class CompositionWorkProbeModuleOneOption
    : CompositionWorkProbeOption<CompositionWorkProbeModuleOne>;

internal sealed class CompositionWorkProbeModuleOneGuide
    : ModuleGuide<
        CompositionWorkProbeModuleOne,
        CompositionWorkProbeModuleOneOption,
        CompositionWorkProbeModuleOneGuide>;

[ModuleKey("Test.Monica.Core.CompositionWork.Two")]
internal sealed class CompositionWorkProbeModuleTwo(CompositionWorkProbeModuleTwoOption option)
    : CompositionWorkProbeModule<
        CompositionWorkProbeModuleTwo,
        CompositionWorkProbeModuleTwoOption,
        CompositionWorkProbeModuleTwoGuide>(option);

internal sealed class CompositionWorkProbeModuleTwoOption
    : CompositionWorkProbeOption<CompositionWorkProbeModuleTwo>;

internal sealed class CompositionWorkProbeModuleTwoGuide
    : ModuleGuide<
        CompositionWorkProbeModuleTwo,
        CompositionWorkProbeModuleTwoOption,
        CompositionWorkProbeModuleTwoGuide>;
