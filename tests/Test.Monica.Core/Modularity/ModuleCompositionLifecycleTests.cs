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
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.State;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleCompositionLifecycleTests
{
    [Fact]
    public void AddMonica_WhenUsingGenericHost_ShouldCompleteAtServiceRegistrationOnce()
    {
        var logFactory = new RecordingLoggerFactory();
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureModuleSystem(options => options.EnableSummaryLog = true);
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModule<CompositionProbeModule, CompositionProbeModuleOption, CompositionProbeModuleGuide>(
                options => options.LoggerFactory = logFactory);
        });

        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();

        application.Profiling.IsRunning.Should().BeFalse();
        logFactory.CountContaining("Module system performance summary:").Should().Be(1);
        logFactory.CountContaining("Module system register order summary:").Should().Be(1);

        application.Modules.CompleteComposition(ModuleCompositionCompletionPoint.ServiceRegistration);

        logFactory.CountContaining("Module system performance summary:").Should().Be(1);
        logFactory.CountContaining("Module system register order summary:").Should().Be(1);
    }

    [Fact]
    public async Task MapMonica_WhenUsingWebHost_ShouldCompleteAtEndpointMapping()
    {
        await using var app = BuildWebApplication();
        var application = app.Services.GetRequiredService<MonicaApplication>();

        application.Profiling.IsRunning.Should().BeTrue();

        app.UseMonica();
        application.Profiling.IsRunning.Should().BeTrue();

        app.MapMonica();
        application.Profiling.IsRunning.Should().BeFalse();
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
            monica.AddModule<CompositionProbeModule, CompositionProbeModuleOption, CompositionProbeModuleGuide>();
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
            nameof(ModulePhase.ClaimDependencies),
            nameof(ModulePhase.InitFinalConfigures),
            nameof(ModulePhase.ConfigureBuilder),
            nameof(ModulePhase.ConfigureServices),
            nameof(ModulePhase.IterateBusinessTypes),
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

        state.FailCompletion(failure);

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

    private static WebApplicationBuilder CreateWebBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        return builder;
    }

    private static void ConfigureMonica(WebApplicationBuilder builder)
    {
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModule<CompositionProbeModule, CompositionProbeModuleOption, CompositionProbeModuleGuide>();
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

[ModuleKey("Test.Monica.Core.CompositionProbe")]
public sealed class CompositionProbeModule(CompositionProbeModuleOption option)
    : ModuleBase<CompositionProbeModule, CompositionProbeModuleOption, CompositionProbeModuleGuide>(option)
{
    public override void ConfigureBuilder(IHostApplicationBuilder builder)
    {
        UseCompositionLoggerFactory(Option.LoggerFactory);
    }
}

public sealed class CompositionProbeModuleGuide
    : ModuleGuide<CompositionProbeModule, CompositionProbeModuleOption, CompositionProbeModuleGuide>;

public sealed class CompositionProbeModuleOption : ModuleOptions<CompositionProbeModule>
{
    public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;
}
