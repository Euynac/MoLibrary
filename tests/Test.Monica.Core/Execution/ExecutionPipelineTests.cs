using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Execution;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Execution;

public sealed class ExecutionPipelineTests
{
    [Fact]
    public void AddMonica_WhenBehaviorKeysAreDuplicated_ShouldRejectComposition()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica => monica
            .AddExecutionPipeline()
            .AddBehavior<OuterBehavior>("duplicate")
            .AddBehavior<AlphaBehavior>("duplicate"));

        compose.Should().Throw<Exception>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage("*Execution behavior key 'duplicate' is already registered*");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldOrderBehaviorsByOrderThenStableKey()
    {
        using var host = BuildHost(guide => guide
            .AddBehavior<OuterBehavior>("outer", order: 0)
            .AddBehavior<BetaBehavior>("beta", order: 100)
            .AddBehavior<AlphaBehavior>("alpha", order: 100));
        using var scope = host.Services.CreateScope();
        var trace = scope.ServiceProvider.GetRequiredService<ExecutionTrace>();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var context = CreateContext(TestContext.Current.CancellationToken);

        var result = await pipeline.ExecuteAsync(context, () =>
        {
            trace.Entries.Add("terminal");
            return Task.FromResult("result");
        });

        result.Should().Be("result");
        trace.Entries.Should().Equal(
            "outer:enter",
            "alpha:enter",
            "beta:enter",
            "terminal",
            "beta:exit",
            "alpha:exit",
            "outer:exit");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFilterAndCacheApplicableRegistrationPlans()
    {
        var filterInvocations = 0;
        using var host = BuildHost(guide => guide.AddBehavior<FilteredBehavior>(
            "filtered",
            descriptorFilter: descriptor =>
            {
                Interlocked.Increment(ref filterInvocations);
                return descriptor.Point == SelectedPoint;
            }));
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var trace = scope.ServiceProvider.GetRequiredService<ExecutionTrace>();
        var selectedContext = CreateContext(TestContext.Current.CancellationToken, SelectedPoint);
        var ignoredContext = CreateContext(
            TestContext.Current.CancellationToken,
            new ExecutionPoint("test.ignored"));

        await pipeline.ExecuteAsync(selectedContext, static () => Task.FromResult("first"));
        await pipeline.ExecuteAsync(selectedContext, static () => Task.FromResult("second"));
        await pipeline.ExecuteAsync(ignoredContext, static () => Task.FromResult("ignored"));

        trace.Entries.Should().Equal("filtered", "filtered");
        filterInvocations.Should().Be(2, "one plan should be built for each distinct descriptor");
    }

    [Fact]
    public async Task ExecuteAsync_WhenBehaviorShortCircuits_ShouldNotInvokeTerminal()
    {
        using var host = BuildHost(guide => guide.AddBehavior<ShortCircuitBehavior>("short-circuit"));
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var terminalInvocations = 0;

        var result = await pipeline.ExecuteAsync(CreateContext(TestContext.Current.CancellationToken), () =>
        {
            Interlocked.Increment(ref terminalInvocations);
            return Task.FromResult("terminal");
        });

        result.Should().Be("short-circuit");
        terminalInvocations.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBehaviorInvokesNextTwice_ShouldRejectSecondInvocation()
    {
        using var host = BuildHost(guide => guide.AddBehavior<DoubleInvocationBehavior>("double"));
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var terminalInvocations = 0;

        Func<Task> execute = async () => await pipeline.ExecuteAsync(
            CreateContext(TestContext.Current.CancellationToken),
            () =>
            {
                Interlocked.Increment(ref terminalInvocations);
                return Task.FromResult("terminal");
            });

        await execute.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*invoked the remaining pipeline more than once*");
        terminalInvocations.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTerminalFails_ShouldPreserveExceptionIdentity()
    {
        using var host = BuildHost(_ => { });
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var expected = new TestExecutionException("terminal failure");

        Func<Task> execute = async () => await pipeline.ExecuteAsync(
            CreateContext(TestContext.Current.CancellationToken),
            () => Task.FromException<string>(expected));

        var assertion = await execute.Should().ThrowAsync<TestExecutionException>();
        assertion.Which.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTerminalIsCanceled_ShouldPreserveCancellationIdentity()
    {
        using var host = BuildHost(_ => { });
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var expected = new OperationCanceledException(cancellation.Token);

        Func<Task> execute = async () => await pipeline.ExecuteAsync(
            CreateContext(cancellation.Token),
            () => Task.FromException<string>(expected));

        var assertion = await execute.Should().ThrowAsync<OperationCanceledException>();
        assertion.Which.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldResolveScopedBehaviorFromThePipelineScope()
    {
        using var host = BuildHost(
            guide => guide.AddBehavior<ScopedBehavior>("scoped", lifetime: ServiceLifetime.Scoped),
            services => services.AddScoped<ScopeIdentity>());

        Guid firstScopeIdentity;
        using (var firstScope = host.Services.CreateScope())
        {
            var expectedIdentity = firstScope.ServiceProvider.GetRequiredService<ScopeIdentity>();
            var pipeline = firstScope.ServiceProvider.GetRequiredService<IExecutionPipeline>();

            var observedFirstIdentity = await pipeline.ExecuteAsync(
                CreateContext(TestContext.Current.CancellationToken),
                () => Task.FromResult(expectedIdentity.Value.ToString()));

            observedFirstIdentity.Should().Be(expectedIdentity.Value.ToString());
            firstScopeIdentity = expectedIdentity.Value;
        }

        using var secondScope = host.Services.CreateScope();
        var secondIdentity = secondScope.ServiceProvider.GetRequiredService<ScopeIdentity>();
        var secondPipeline = secondScope.ServiceProvider.GetRequiredService<IExecutionPipeline>();

        var observedSecondIdentity = await secondPipeline.ExecuteAsync(
            CreateContext(TestContext.Current.CancellationToken),
            () => Task.FromResult(secondIdentity.Value.ToString()));

        observedSecondIdentity.Should().Be(secondIdentity.Value.ToString());
        secondIdentity.Value.Should().NotBe(firstScopeIdentity);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCloseAndResolveOpenGenericBehavior()
    {
        using var host = BuildHost(guide => guide.AddBehavior(
            "open",
            typeof(OpenGenericBehavior<,>)));
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var trace = scope.ServiceProvider.GetRequiredService<ExecutionTrace>();

        var result = await pipeline.ExecuteAsync(
            CreateContext(TestContext.Current.CancellationToken),
            static () => Task.FromResult("result"));

        result.Should().Be("result");
        trace.Entries.Should().ContainSingle()
            .Which.Should().Be($"open:{nameof(TestRequest)}:{nameof(String)}");
    }

    [Fact]
    public async Task ExecuteAsync_WhenOpenBehaviorConstraintsDoNotMatch_ShouldExcludeBehavior()
    {
        using var host = BuildHost(guide => guide.AddBehavior(
            "constrained",
            typeof(SelfTypedBehavior<,>)));
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var trace = scope.ServiceProvider.GetRequiredService<ExecutionTrace>();

        var result = await pipeline.ExecuteAsync(
            CreateContext(TestContext.Current.CancellationToken),
            static () => Task.FromResult("result"));

        result.Should().Be("result");
        trace.Entries.Should().BeEmpty();
    }

    private static readonly ExecutionPoint SelectedPoint = new("test.selected");

    private static IHost BuildHost(
        Action<ModuleExecutionPipelineGuide> configure,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddScoped<ExecutionTrace>();
        configureServices?.Invoke(builder.Services);
        builder.AddMonica(monica => configure(monica.AddExecutionPipeline()));
        return builder.Build();
    }

    private static ExecutionContext<TestRequest> CreateContext(
        CancellationToken cancellationToken,
        ExecutionPoint? point = null)
    {
        var descriptor = new ExecutionDescriptor(
            point ?? SelectedPoint,
            "test.operation",
            typeof(ExecutionPipelineTests),
            null,
            typeof(TestRequest),
            typeof(string),
            isBusinessOperation: true,
            isLongRunning: false);
        return new ExecutionContext<TestRequest>(descriptor, new TestRequest("input"), cancellationToken: cancellationToken);
    }

    private sealed record TestRequest(string Value);

    private sealed class ExecutionTrace
    {
        public List<string> Entries { get; } = [];
    }

    private abstract class RecordingBehavior(ExecutionTrace trace, string name)
        : IExecutionBehavior<TestRequest, string>
    {
        public async Task<string> ExecuteAsync(
            ExecutionContext<TestRequest> context,
            ExecutionDelegate<string> next)
        {
            trace.Entries.Add($"{name}:enter");
            var result = await next();
            trace.Entries.Add($"{name}:exit");
            return result;
        }
    }

    private sealed class OuterBehavior(ExecutionTrace trace) : RecordingBehavior(trace, "outer");

    private sealed class AlphaBehavior(ExecutionTrace trace) : RecordingBehavior(trace, "alpha");

    private sealed class BetaBehavior(ExecutionTrace trace) : RecordingBehavior(trace, "beta");

    private sealed class FilteredBehavior(ExecutionTrace trace) : IExecutionBehavior<TestRequest, string>
    {
        public async Task<string> ExecuteAsync(
            ExecutionContext<TestRequest> context,
            ExecutionDelegate<string> next)
        {
            trace.Entries.Add("filtered");
            return await next();
        }
    }

    private sealed class ShortCircuitBehavior : IExecutionBehavior<TestRequest, string>
    {
        public Task<string> ExecuteAsync(
            ExecutionContext<TestRequest> context,
            ExecutionDelegate<string> next)
        {
            return Task.FromResult("short-circuit");
        }
    }

    private sealed class DoubleInvocationBehavior : IExecutionBehavior<TestRequest, string>
    {
        public async Task<string> ExecuteAsync(
            ExecutionContext<TestRequest> context,
            ExecutionDelegate<string> next)
        {
            await next();
            return await next();
        }
    }

    private sealed class ScopeIdentity
    {
        public Guid Value { get; } = Guid.NewGuid();
    }

    private sealed class ScopedBehavior(ScopeIdentity identity) : IExecutionBehavior<TestRequest, string>
    {
        public async Task<string> ExecuteAsync(
            ExecutionContext<TestRequest> context,
            ExecutionDelegate<string> next)
        {
            await next();
            return identity.Value.ToString();
        }
    }

    private sealed class OpenGenericBehavior<TInput, TResult>(ExecutionTrace trace)
        : IExecutionBehavior<TInput, TResult>
    {
        public async Task<TResult> ExecuteAsync(
            ExecutionContext<TInput> context,
            ExecutionDelegate<TResult> next)
        {
            trace.Entries.Add($"open:{typeof(TInput).Name}:{typeof(TResult).Name}");
            return await next();
        }
    }

    private interface ISelfTypedResult<TSelf>
    {
    }

    private sealed class SelfTypedBehavior<TInput, TResult>(ExecutionTrace trace)
        : IExecutionBehavior<TInput, TResult>
        where TResult : class, ISelfTypedResult<TResult>
    {
        public async Task<TResult> ExecuteAsync(
            ExecutionContext<TInput> context,
            ExecutionDelegate<TResult> next)
        {
            trace.Entries.Add("constrained");
            return await next();
        }
    }

    private sealed class TestExecutionException(string message) : Exception(message);
}
