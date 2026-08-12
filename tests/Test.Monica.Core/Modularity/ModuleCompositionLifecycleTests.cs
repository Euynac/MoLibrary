using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Diagnostics.Services;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.State;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleCompositionLifecycleTests
{
    private const string SELECTED_WEB_FEATURE_REQUIREMENT =
        "The selected probe middleware requires an ASP.NET Core request pipeline.";

    [Fact]
    public void AddMonica_WhenNoModulesAreRegistered_ShouldStillCompleteOneCompositionTimeline()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(static _ => Thread.Sleep(25));

        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();
        var composition = application.Profiling.GetCompositionPerformance();

        application.Profiling.IsRunning.Should().BeFalse();
        composition.Milestones.Select(static milestone => milestone.Milestone).Should().Equal(
            ModuleCompositionMilestone.CompositionStarted,
            ModuleCompositionMilestone.ServiceRegistrationCompleted,
            ModuleCompositionMilestone.CompositionCompleted);
        composition.ServiceRegistrationDurationMs.Should().BeGreaterThanOrEqualTo(20);
        composition.ServiceRegistration.ApplicationConfigurationDurationMs.Should().BeGreaterThanOrEqualTo(20);
        composition.SystemPhases.Should().ContainSingle(phase =>
            phase.PhaseName == "ApplicationConfiguration"
            && phase.DurationMs >= 20);
        composition.ModulePhaseExecutions.Should().BeEmpty();

        var diagnostics = CreateDiagnostics(host.Services);
        diagnostics.IsFinal.Should().BeTrue();
        diagnostics.Outcome.Should().Be(ModuleCompositionOutcome.Succeeded);
        diagnostics.Summary.ModuleCount.Should().Be(0);
        diagnostics.Summary.ServiceRegistrationDurationMs.Should().Be(composition.ServiceRegistrationDurationMs);
    }

    [Fact]
    public void AddMonica_WhenUsingGenericHost_ShouldCompleteAtServiceRegistrationOnce()
    {
        var logFactory = new RecordingLoggerFactory();
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureModuleSystem(options => options.EnableSummaryLog = true);
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModule<CompositionProbeModule, CompositionProbeModuleOption>(
                options => options.LoggerFactory = logFactory);
        });

        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();

        application.Profiling.IsRunning.Should().BeFalse();
        logFactory.CountContaining("Module system performance summary:").Should().Be(1);
        logFactory.CountContaining("Module system register order summary:").Should().Be(1);
        var composition = application.Profiling.GetCompositionPerformance();
        composition.Milestones.Select(static milestone => milestone.Milestone).Should().Equal(
            ModuleCompositionMilestone.CompositionStarted,
            ModuleCompositionMilestone.ServiceRegistrationCompleted,
            ModuleCompositionMilestone.CompositionCompleted);
        composition.Milestones.Select(static milestone => milestone.OffsetMs).Should().BeInAscendingOrder();
        composition.Initialization.HostOwnedDurationMs.Should().Be(0);
        (composition.Initialization.MonicaFrameworkDurationMs
         + composition.Initialization.ApplicationConfigurationDurationMs)
            .Should().BeApproximately(composition.Initialization.TotalDurationMs, 0.001);
        (composition.ServiceRegistration.ApplicationConfigurationDurationMs
         + composition.ServiceRegistration.SerialModuleCallbackDurationMs
         + composition.ServiceRegistration.BlockingWaitDurationMs
         + composition.ServiceRegistration.OrchestrationDurationMs)
            .Should().BeApproximately(composition.ServiceRegistration.TotalDurationMs, 0.001);
        composition.SystemPhases.Should().Contain(phase =>
            phase.PhaseName == $"{nameof(ModulePhase.ConfigureBuilder)} / {nameof(ModulePhase.ConfigureServices)}");
        composition.StartupWorkItems.Should().BeEmpty();
        composition.StartupWorkBarriers.Should().OnlyContain(static checkpoint => checkpoint.PendingWorkItemCount == 0);
        composition.AggregateBarrierWaitDurationMs.Should().Be(0);
        composition.CriticalBarrier.Should().BeNull();
        composition.CriticalWorkItem.Should().BeNull();
        application.Profiling.GetPerformanceSummary().Should()
            .Contain("Module system initialization elapsed:")
            .And.Contain("Monica framework work:")
            .And.Contain("Application module configuration:")
            .And.Contain("Host-owned gaps:")
            .And.Contain("Service registration elapsed:")
            .And.NotContain("End-to-end composition elapsed:");

        var diagnostics = CreateDiagnostics(host.Services);
        diagnostics.Summary.ServiceRegistrationDurationMs.Should().Be(composition.ServiceRegistrationDurationMs);
        diagnostics.Summary.PerformanceBudgets.Should().BeEmpty();

        application.Modules.CompleteComposition(ModuleCompositionCompletionPoint.ServiceRegistration);

        logFactory.CountContaining("Module system performance summary:").Should().Be(1);
        logFactory.CountContaining("Module system register order summary:").Should().Be(1);
    }

    [Fact]
    public void AddMonica_WhenOptionalWebCapabilityUsesGenericHost_ShouldKeepNonWebRegistrations()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModule<OptionalWebProbeModule, OptionalWebProbeModuleOption>();
        });

        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();

        host.Services.GetRequiredService<OptionalWebProbeService>().Should().NotBeNull();
        application.Modules.RuntimeSnapshots.Should().ContainSingle(snapshot =>
            snapshot.ModuleType == typeof(OptionalWebProbeModule)
            && snapshot.IsWebModule
            && !snapshot.RequiresWebHost);
    }

    [Fact]
    public void AddMonica_WhenModuleRequiresWebHost_ShouldFailBeforeServiceCollectionMutation()
    {
        var builder = Host.CreateApplicationBuilder();
        var servicesBeforeComposition = builder.Services.ToArray();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModule<HostRequiredWebProbeModule, HostRequiredWebProbeModuleOption>();
        });

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage($"*WebApplicationBuilder*{nameof(HostRequiredWebProbeModule)}*");
        builder.Services.Should().Equal(servicesBeforeComposition);
    }

    [Fact]
    public void AddMonica_WhenSelectedWebFeatureRequiresHost_ShouldFailWithReasonBeforeMutation()
    {
        var builder = Host.CreateApplicationBuilder();
        var servicesBeforeComposition = builder.Services.ToArray();

        Action compose = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModule<OptionalWebProbeModule, OptionalWebProbeModuleOption>()
                .RequireWebHost(SELECTED_WEB_FEATURE_REQUIREMENT)
                .ConfigureApplicationBuilder(static _ => { });
        });

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage($"*{nameof(OptionalWebProbeModule)}*{SELECTED_WEB_FEATURE_REQUIREMENT}*");
        builder.Services.Should().Equal(servicesBeforeComposition);
    }

    [Fact]
    public async Task AddMonica_WhenSelectedWebFeatureUsesWebHost_ShouldExposeRequirementDiagnostics()
    {
        var builder = CreateWebBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModule<OptionalWebProbeModule, OptionalWebProbeModuleOption>()
                .RequireWebHost(SELECTED_WEB_FEATURE_REQUIREMENT)
                .ConfigureApplicationBuilder(static _ => { });
        });
        await using var app = builder.Build();
        var application = app.Services.GetRequiredService<MonicaApplication>();

        var snapshot = application.Modules.RuntimeSnapshots.Should().ContainSingle(snapshot =>
            snapshot.ModuleType == typeof(OptionalWebProbeModule)).Subject;
        var detail = CreateDiagnostics(app.Services).Modules.Single(module =>
            module.TypeName == nameof(OptionalWebProbeModule));

        snapshot.RequiresWebHost.Should().BeTrue();
        snapshot.WebHostRequirementReason.Should().Be(SELECTED_WEB_FEATURE_REQUIREMENT);
        detail.RequiresWebHost.Should().BeTrue();
        detail.WebHostRequirementReason.Should().Be(SELECTED_WEB_FEATURE_REQUIREMENT);
    }

    [Fact]
    public void GetDependencyGraph_WhenUIIdentityIsExplicit_ShouldIgnoreTypeNameSuffixes()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModule<ExplicitPresentationProbeModule, ExplicitPresentationProbeModuleOption>();
            monica.AddModule<NonUiSuffixProbeModuleUI, NonUiSuffixProbeModuleUIOption>();
        });
        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();

        var nodes = CreateDiagnostics(host.Services).Modules;

        nodes.Should().ContainSingle(node =>
            node.TypeName == nameof(ExplicitPresentationProbeModule) && node.IsUiModule);
        nodes.Should().ContainSingle(node =>
            node.TypeName == nameof(NonUiSuffixProbeModuleUI) && !node.IsUiModule);
    }

    [Fact]
    public async Task MapMonica_WhenUsingWebHost_ShouldCompleteAtEndpointMapping()
    {
        await using var app = BuildWebApplication();
        var application = app.Services.GetRequiredService<MonicaApplication>();
        var serviceRegistrationDuration = application.Profiling
            .GetCompositionPerformance()
            .ServiceRegistrationDurationMs;

        application.Profiling.IsRunning.Should().BeTrue();

        await Task.Delay(25, TestContext.Current.CancellationToken);
        app.UseMonica();
        application.Profiling.IsRunning.Should().BeTrue();
        var afterUse = application.Profiling.GetCompositionPerformance();
        afterUse.Milestones.Select(static milestone => milestone.Milestone).Should().Equal(
            ModuleCompositionMilestone.CompositionStarted,
            ModuleCompositionMilestone.ServiceRegistrationCompleted,
            ModuleCompositionMilestone.ApplicationPipelineStarted,
            ModuleCompositionMilestone.ApplicationPipelineCompleted);

        await Task.Delay(25, TestContext.Current.CancellationToken);
        app.MapMonica();
        application.Profiling.IsRunning.Should().BeFalse();
        var composition = application.Profiling.GetCompositionPerformance();
        composition.Milestones.Select(static milestone => milestone.Milestone).Should().Equal(
            ModuleCompositionMilestone.CompositionStarted,
            ModuleCompositionMilestone.ServiceRegistrationCompleted,
            ModuleCompositionMilestone.ApplicationPipelineStarted,
            ModuleCompositionMilestone.ApplicationPipelineCompleted,
            ModuleCompositionMilestone.EndpointMappingStarted,
            ModuleCompositionMilestone.CompositionCompleted);
        composition.Milestones.Select(static milestone => milestone.OffsetMs).Should().BeInAscendingOrder();
        composition.ServiceRegistrationDurationMs.Should().Be(serviceRegistrationDuration);
        composition.ElapsedDurationMs.Should().BeGreaterThan(serviceRegistrationDuration + 30);
        composition.Initialization.HostOwnedDurationMs.Should().BeGreaterThan(30);
        (composition.Initialization.MonicaFrameworkDurationMs
         + composition.Initialization.ApplicationConfigurationDurationMs
         + composition.Initialization.HostOwnedDurationMs)
            .Should().BeApproximately(composition.Initialization.TotalDurationMs, 0.001);

        var webModule = composition.ModulePhaseExecutions
            .Where(static execution => execution.ModuleTypeName == nameof(CompositionWebProbeModule))
            .Where(static execution => execution.Phase == ModulePhase.ConfigureApplicationBuilder)
            .ToArray();
        webModule.Should().ContainSingle().Which.Kind.Should().Be(ModuleCallbackKind.Lifecycle);
    }

    [Fact]
    public async Task UseMonica_WhenWebContributionsAreDeclaredOutOfStageOrder_ShouldHonorNamedRoutingStages()
    {
        var stages = new List<string>();
        var builder = CreateWebBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            var registration = monica.AddModule<WebStageProbeModule, WebStageProbeModuleOption>(
                options => options.Stages = stages);
            registration.ConfigureApplicationBuilder(
                _ => stages.Add("after-routing"),
                ModuleWebStage.AfterRouting);
            registration.ConfigureApplicationBuilder(
                _ => stages.Add("before-routing"),
                ModuleWebStage.BeforeRouting);
        });
        await using var app = builder.Build();

        app.UseMonica();
        app.MapMonica();

        stages.Should().Equal("module-before-routing", "before-routing", "after-routing");
    }

    [Fact]
    public async Task UseAndMapMonica_WhenCalledOutOfOrderOrRepeated_ShouldRejectReplay()
    {
        await using var app = BuildWebApplication();

        Action mapBeforeUse = () => app.MapMonica();
        mapBeforeUse.Should().Throw<InvalidOperationException>()
            .WithMessage("*UseMonica()*before*MapMonica()*");

        app.UseMonica();

        Action duplicateUse = () => app.UseMonica();
        duplicateUse.Should().Throw<InvalidOperationException>()
            .WithMessage("*UseMonica()*only once*");

        app.MapMonica();

        Action duplicateMap = () => app.MapMonica();
        duplicateMap.Should().Throw<InvalidOperationException>()
            .WithMessage("*MapMonica()*only once*");
    }

    [Fact]
    public async Task MapMonica_WhenApplicationBuilderDiffersFromUseMonica_ShouldRejectBuilderMismatch()
    {
        await using var app = BuildWebApplication();
        app.UseMonica();
        IApplicationBuilder branch = new ApplicationBuilder(app.Services);

        Action mapBranch = () => branch.MapMonica();

        mapBranch.Should().Throw<InvalidOperationException>()
            .WithMessage("*same application builder instance*");
        app.MapMonica();
    }

    [Fact]
    public async Task MapMonica_WhenEndpointValidationFails_ShouldStopWithoutCompletionMilestone()
    {
        var builder = CreateWebBuilder();
        ConfigureMonica(builder, failEndpointMapping: true);
        await using var app = builder.Build();
        var application = app.Services.GetRequiredService<MonicaApplication>();
        app.UseMonica();

        Action map = () => app.MapMonica();

        map.Should().Throw<Exception>().Which.ToString().Should().Contain("endpoint-composition-failure");
        application.Profiling.IsRunning.Should().BeFalse();
        application.Profiling.GetCompositionPerformance()
            .Milestones.Select(static milestone => milestone.Milestone)
            .Should().NotContain(ModuleCompositionMilestone.CompositionCompleted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartAsync_WhenWebCompositionIsIncomplete_ShouldFailBeforeLifecycleParticipants(
        bool useMonica)
    {
        var lifecycle = new TrackingLifecycleService();
        var builder = CreateWebBuilder();
        builder.Services.Configure<HostOptions>(options => options.ServicesStartConcurrently = true);
        builder.Services.AddSingleton<IHostedService>(lifecycle);
        ConfigureMonica(builder);
        await using var app = builder.Build();
        if (useMonica)
        {
            app.UseMonica();
        }

        Func<Task> start = () => app.StartAsync(TestContext.Current.CancellationToken);

        await start.Should().ThrowAsync<OptionsValidationException>()
            .WithMessage("*Monica Web composition is incomplete*MapMonica()*");
        lifecycle.StartingCount.Should().Be(0);
        lifecycle.StartCount.Should().Be(0);
    }

    [Fact]
    public void ResetModuleState_AfterComposition_ShouldClearCompletionAndProfilingState()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModule<CompositionProbeModule, CompositionProbeModuleOption>();
        });
        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();

        application.ResetModuleState();

        application.Modules.RuntimeSnapshots.Should().BeEmpty();
        application.Profiling.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void ModulePhase_ShouldContainCompositionPhasesOnly()
    {
        Enum.GetNames<ModulePhase>().Should().Equal(
            nameof(ModulePhase.None),
            nameof(ModulePhase.Describe),
            nameof(ModulePhase.FinalizeOptions),
            nameof(ModulePhase.DeclareContracts),
            nameof(ModulePhase.ConfigureBuilder),
            nameof(ModulePhase.ConfigureServices),
            nameof(ModulePhase.DeclareTypeDiscovery),
            nameof(ModulePhase.PostConfigureServices),
            nameof(ModulePhase.ConfigureApplicationBuilder),
            nameof(ModulePhase.ConfigureEndpoints),
            nameof(ModulePhase.Disabled));
    }

    [Fact]
    public void CompleteComposition_WhenFinalizationFails_ShouldRecordATerminalFailure()
    {
        var state = new ModuleCompositionState();
        state.Initialize(Host.CreateApplicationBuilder());
        state.TryBeginCompletion(ModuleCompositionCompletionPoint.ServiceRegistration).Should().BeTrue();
        var failure = new InvalidOperationException("Final module validation failed.");

        state.FailCompletion(failure, ModuleCompositionFailureKind.Completion);

        state.GetStartupValidationFailure().Should()
            .Contain("composition failed")
            .And.Contain("Final module validation failed.");

        Action retry = () => state.TryBeginCompletion(ModuleCompositionCompletionPoint.ServiceRegistration);
        retry.Should().Throw<InvalidOperationException>()
            .WithMessage("*previously failed*")
            .WithInnerException<InvalidOperationException>()
            .WithMessage("Final module validation failed.");
    }

    private static WebApplication BuildWebApplication()
    {
        var builder = CreateWebBuilder();
        ConfigureMonica(builder);
        return builder.Build();
    }

    private static ModuleDiagnosticsSnapshot CreateDiagnostics(IServiceProvider services)
    {
        return new ModuleDiagnosticsService(
                services.GetRequiredService<MonicaApplication>(),
                Options.Create(new ModuleSystemOption()),
                services.GetRequiredService<IHostEnvironment>())
            .GetSnapshot();
    }

    private static WebApplicationBuilder CreateWebBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        return builder;
    }

    private static void ConfigureMonica(WebApplicationBuilder builder, bool failEndpointMapping = false)
    {
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModule<CompositionProbeModule, CompositionProbeModuleOption>();
            monica.AddModule<
                CompositionWebProbeModule,
                CompositionWebProbeModuleOption>(options => options.FailEndpointMapping = failEndpointMapping);
        });
    }

    private sealed class TrackingLifecycleService : IHostedLifecycleService
    {
        public int StartingCount { get; private set; }

        public int StartCount { get; private set; }

        public Task StartingAsync(CancellationToken cancellationToken)
        {
            StartingCount++;
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCount++;
            return Task.CompletedTask;
        }

        public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        private readonly ConcurrentQueue<string> _messages = [];

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(_messages);

        public void Dispose()
        {
        }

        public int CountContaining(string value)
        {
            return _messages.Count(message => message.Contains(value, StringComparison.Ordinal));
        }

        private sealed class RecordingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(formatter(state, exception));
            }
        }
    }
}

internal sealed class CompositionProbeModule : MonicaModule<CompositionProbeModuleOption>
{
    public override void ConfigureBuilder(ModuleBuilderContext<CompositionProbeModuleOption> context)
    {
        UseCompositionLoggerFactory(Option.LoggerFactory);
    }
}

internal sealed class CompositionProbeModuleOption : ModuleOptions<CompositionProbeModule>
{
    public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;
}

internal sealed class CompositionWebProbeModule
    : MonicaModule<CompositionWebProbeModuleOption>, IWebHostRequiredModule
{
    public override void ConfigureEndpoints(WebModuleContext<CompositionWebProbeModuleOption> context)
    {
        if (Option.FailEndpointMapping)
        {
            throw new InvalidOperationException("endpoint-composition-failure");
        }
    }
}

internal sealed class CompositionWebProbeModuleOption : ModuleOptions<CompositionWebProbeModule>
{
    public bool FailEndpointMapping { get; set; }
}

internal sealed class OptionalWebProbeModule : MonicaModule<OptionalWebProbeModuleOption>, IWebModule
{
    public override void ConfigureServices(ModuleContext<OptionalWebProbeModuleOption> context)
    {
        context.Services.AddSingleton<OptionalWebProbeService>();
    }

    public override void ConfigureApplicationBuilder(WebModuleContext<OptionalWebProbeModuleOption> context)
    {
        throw new InvalidOperationException("A generic host must omit optional web contributions.");
    }
}

internal sealed class OptionalWebProbeModuleOption : ModuleOptions<OptionalWebProbeModule>;

internal sealed class OptionalWebProbeService;

internal sealed class HostRequiredWebProbeModule
    : MonicaModule<HostRequiredWebProbeModuleOption>, IWebHostRequiredModule
{
    public override void ConfigureServices(ModuleContext<HostRequiredWebProbeModuleOption> context)
    {
        context.Services.AddSingleton<HostRequiredWebProbeService>();
    }
}

internal sealed class HostRequiredWebProbeModuleOption : ModuleOptions<HostRequiredWebProbeModule>;

internal sealed class HostRequiredWebProbeService;

internal sealed class WebStageProbeModule
    : MonicaModule<WebStageProbeModuleOption>, IWebHostRequiredModule
{
    public override void ConfigureApplicationBuilder(WebModuleContext<WebStageProbeModuleOption> context)
    {
        Option.Stages?.Add("module-before-routing");
    }
}

internal sealed class WebStageProbeModuleOption : ModuleOptions<WebStageProbeModule>
{
    public List<string>? Stages { get; set; }
}

internal sealed class ExplicitPresentationProbeModule
    : MonicaModule<ExplicitPresentationProbeModuleOption>, IUIModule;

internal sealed class ExplicitPresentationProbeModuleOption : ModuleOptions<ExplicitPresentationProbeModule>;

internal sealed class NonUiSuffixProbeModuleUI : MonicaModule<NonUiSuffixProbeModuleUIOption>;

internal sealed class NonUiSuffixProbeModuleUIOption : ModuleOptions<NonUiSuffixProbeModuleUI>;
