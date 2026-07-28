using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Execution;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Execution;

public sealed class ExecutionPipelineTests
{
    private static readonly Task<string> CACHED_RESULT = Task.FromResult("result");
    private static readonly ExecutionDelegate<string> CACHED_RESULT_TERMINAL = static () => CACHED_RESULT;
    private static readonly Func<Task> CACHED_UNIT_TERMINAL = static () => Task.CompletedTask;

    [Fact]
    public void AddMonica_WhenBehaviorImplementationIsDuplicated_ShouldRejectComposition()
    {
        var builder = Host.CreateApplicationBuilder();

        Action compose = () => builder.AddMonica(monica => monica
            .AddExecutionPipeline()
            .AddBehavior<OuterBehavior>()
            .AddBehavior<OuterBehavior>());

        compose.Should().Throw<Exception>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage($"*Execution behavior '{typeof(OuterBehavior).FullName}' is already registered*");
    }

    [Fact]
    public void AddMonica_WhenBehaviorHasIndependentServiceRegistration_ShouldRejectComposition()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddTransient<OuterBehavior>();

        Action compose = () => builder.AddMonica(monica => monica
            .AddExecutionPipeline()
            .AddBehavior<OuterBehavior>());

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage("*must have exactly one service registration, owned by AddBehavior*");
    }

    [Fact]
    public void Build_WhenBehaviorIsRegisteredAgainAfterComposition_ShouldRejectResolution()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica => monica
            .AddExecutionPipeline()
            .AddBehavior<OuterBehavior>());
        builder.Services.AddTransient<OuterBehavior>();
        using var host = builder.Build();

        Action resolve = () => _ = host.Services.GetRequiredService<IExecutionPipeline>();

        resolve.Should().Throw<InvalidOperationException>()
            .WithMessage("*must have exactly one service registration, owned by AddBehavior*");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldOrderBehaviorsByOrderThenStableTypeName()
    {
        using var host = BuildHost(guide => guide
            .AddBehavior<OuterBehavior>(order: 0)
            .AddBehavior<BetaBehavior>(order: 100)
            .AddBehavior<AlphaBehavior>(order: 100));
        using var scope = host.Services.CreateScope();
        var trace = scope.ServiceProvider.GetRequiredService<ExecutionTrace>();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var descriptor = CreateDescriptor();

        var result = await ExecuteAsync(pipeline, descriptor, () =>
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
            descriptorFilter: descriptor =>
            {
                Interlocked.Increment(ref filterInvocations);
                return descriptor.Point == SelectedPoint;
            }));
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var trace = scope.ServiceProvider.GetRequiredService<ExecutionTrace>();
        var selectedDescriptor = CreateDescriptor();
        var ignoredDescriptor = CreateDescriptor(new ExecutionPoint("test.ignored"));

        await ExecuteAsync(pipeline, selectedDescriptor, static () => Task.FromResult("first"));
        await ExecuteAsync(pipeline, selectedDescriptor, static () => Task.FromResult("second"));
        await ExecuteAsync(pipeline, ignoredDescriptor, static () => Task.FromResult("ignored"));

        trace.Entries.Should().Equal("filtered", "filtered");
        filterInvocations.Should().Be(2, "one plan should be built for each distinct descriptor");
    }

    [Fact]
    public void ExecuteAsync_WhenNoBehaviorApplies_ShouldUseAllocationFreeResultFastPath()
    {
        using var host = BuildHost(_ => { });
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var descriptor = CreateDescriptor();
        var input = new TestRequest("input");

        var allocatedBytes = MeasureResultFastPathAllocations(
            pipeline,
            descriptor,
            input,
            TestContext.Current.CancellationToken);

        allocatedBytes.Should().Be(0);
    }

    [Fact]
    public void ExecuteAsync_WhenNoBehaviorApplies_ShouldUseAllocationFreeUnitFastPath()
    {
        using var host = BuildHost(_ => { });
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var descriptor = ExecutionDescriptor.ForMethod<TestRequest, ExecutionUnit>(
            SelectedPoint,
            typeof(ExecutionPipelineTests),
            entryMethod: null,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.None);
        var input = new TestRequest("input");

        var allocatedBytes = MeasureUnitFastPathAllocations(
            pipeline,
            descriptor,
            input,
            TestContext.Current.CancellationToken);

        allocatedBytes.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBehaviorDoesNotUseFeatures_ShouldKeepFeaturesLazy()
    {
        using var host = BuildHost(
            guide => guide.AddBehavior<ContextCaptureBehavior>(),
            services => services.AddScoped<ContextCapture>());
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var capture = scope.ServiceProvider.GetRequiredService<ContextCapture>();

        _ = await ExecuteAsync(
            pipeline,
            CreateDescriptor(),
            static () => Task.FromResult("result"));

        capture.Context.Should().NotBeNull();
        var context = capture.Context!;
        var featuresField = typeof(ExecutionContext<TestRequest>).GetField(
            "_features",
            BindingFlags.Instance | BindingFlags.NonPublic);
        featuresField.Should().NotBeNull();
        featuresField!.GetValue(context).Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenBehaviorShortCircuits_ShouldNotInvokeTerminal()
    {
        using var host = BuildHost(guide => guide.AddBehavior<ShortCircuitBehavior>());
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var terminalInvocations = 0;

        var result = await ExecuteAsync(pipeline, CreateDescriptor(), () =>
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
        using var host = BuildHost(guide => guide.AddBehavior<DoubleInvocationBehavior>());
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var terminalInvocations = 0;

        Func<Task> execute = async () => await ExecuteAsync(
            pipeline,
            CreateDescriptor(),
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
    public async Task ExecuteAsync_WhenBehaviorInvokesNextConcurrently_ShouldAllowOnlyOneInvocation()
    {
        using var host = BuildHost(guide => guide.AddBehavior<ConcurrentDoubleInvocationBehavior>());
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var terminalInvocations = 0;

        Func<Task> execute = async () => await ExecuteAsync(
            pipeline,
            CreateDescriptor(),
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

        Func<Task> execute = async () => await ExecuteAsync(
            pipeline,
            CreateDescriptor(),
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

        Func<Task> execute = async () => await ExecuteAsync(
            pipeline,
            CreateDescriptor(),
            () => Task.FromException<string>(expected));

        var assertion = await execute.Should().ThrowAsync<OperationCanceledException>();
        assertion.Which.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldResolveScopedBehaviorFromThePipelineScope()
    {
        using var host = BuildHost(
            guide => guide.AddBehavior<ScopedBehavior>(lifetime: ServiceLifetime.Scoped),
            services => services.AddScoped<ScopeIdentity>());

        Guid firstScopeIdentity;
        using (var firstScope = host.Services.CreateScope())
        {
            var expectedIdentity = firstScope.ServiceProvider.GetRequiredService<ScopeIdentity>();
            var pipeline = firstScope.ServiceProvider.GetRequiredService<IExecutionPipeline>();

            var observedFirstIdentity = await ExecuteAsync(
                pipeline,
                CreateDescriptor(),
                () => Task.FromResult(expectedIdentity.Value.ToString()));

            observedFirstIdentity.Should().Be(expectedIdentity.Value.ToString());
            firstScopeIdentity = expectedIdentity.Value;
        }

        using var secondScope = host.Services.CreateScope();
        var secondIdentity = secondScope.ServiceProvider.GetRequiredService<ScopeIdentity>();
        var secondPipeline = secondScope.ServiceProvider.GetRequiredService<IExecutionPipeline>();

        var observedSecondIdentity = await ExecuteAsync(
            secondPipeline,
            CreateDescriptor(),
            () => Task.FromResult(secondIdentity.Value.ToString()));

        observedSecondIdentity.Should().Be(secondIdentity.Value.ToString());
        secondIdentity.Value.Should().NotBe(firstScopeIdentity);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCloseAndResolveOpenGenericBehavior()
    {
        using var host = BuildHost(guide => guide.AddBehavior(
            typeof(OpenGenericBehavior<,>)));
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var trace = scope.ServiceProvider.GetRequiredService<ExecutionTrace>();

        var result = await ExecuteAsync(
            pipeline,
            CreateDescriptor(),
            static () => Task.FromResult("result"));

        result.Should().Be("result");
        trace.Entries.Should().ContainSingle()
            .Which.Should().Be($"open:{nameof(TestRequest)}:{nameof(String)}");
    }

    [Fact]
    public async Task ExecuteAsync_WhenOpenBehaviorConstraintsDoNotMatch_ShouldExcludeBehavior()
    {
        using var host = BuildHost(guide => guide.AddBehavior(
            typeof(SelfTypedBehavior<,>)));
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var trace = scope.ServiceProvider.GetRequiredService<ExecutionTrace>();

        var result = await ExecuteAsync(
            pipeline,
            CreateDescriptor(),
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

    private static ExecutionDescriptor CreateDescriptor(ExecutionPoint? point = null)
    {
        return ExecutionDescriptor.ForMethod<TestRequest, string>(
            point ?? SelectedPoint,
            typeof(ExecutionPipelineTests),
            null,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);
    }

    private static Task<string> ExecuteAsync(
        IExecutionPipeline pipeline,
        ExecutionDescriptor descriptor,
        ExecutionDelegate<string> terminal)
    {
        return pipeline.ExecuteAsync(
            descriptor,
            new TestRequest("input"),
            target: null,
            terminal,
            TestContext.Current.CancellationToken);
    }

    private static long MeasureResultFastPathAllocations(
        IExecutionPipeline pipeline,
        ExecutionDescriptor descriptor,
        TestRequest input,
        CancellationToken cancellationToken)
    {
        pipeline.ExecuteAsync(
                descriptor,
                input,
                target: null,
                CACHED_RESULT_TERMINAL,
                cancellationToken)
            .GetAwaiter()
            .GetResult();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 100; index++)
        {
            pipeline.ExecuteAsync(
                    descriptor,
                    input,
                    target: null,
                    CACHED_RESULT_TERMINAL,
                    cancellationToken)
                .GetAwaiter()
                .GetResult();
        }

        return GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    }

    private static long MeasureUnitFastPathAllocations(
        IExecutionPipeline pipeline,
        ExecutionDescriptor descriptor,
        TestRequest input,
        CancellationToken cancellationToken)
    {
        pipeline.ExecuteAsync(
                descriptor,
                input,
                target: null,
                CACHED_UNIT_TERMINAL,
                cancellationToken)
            .GetAwaiter()
            .GetResult();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 100; index++)
        {
            pipeline.ExecuteAsync(
                    descriptor,
                    input,
                    target: null,
                    CACHED_UNIT_TERMINAL,
                    cancellationToken)
                .GetAwaiter()
                .GetResult();
        }

        return GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    }

    private sealed record TestRequest(string Value);

    private sealed class ExecutionTrace
    {
        public List<string> Entries { get; } = [];
    }

    private sealed class ContextCapture
    {
        public ExecutionContext<TestRequest>? Context { get; set; }
    }

    private sealed class ContextCaptureBehavior(ContextCapture capture)
        : IExecutionBehavior<TestRequest, string>
    {
        public Task<string> ExecuteAsync(
            ExecutionContext<TestRequest> context,
            ExecutionDelegate<string> next)
        {
            capture.Context = context;
            return next();
        }
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

    private sealed class ConcurrentDoubleInvocationBehavior : IExecutionBehavior<TestRequest, string>
    {
        public async Task<string> ExecuteAsync(
            ExecutionContext<TestRequest> context,
            ExecutionDelegate<string> next)
        {
            var invocations = new[] { InvokeAsync(next), InvokeAsync(next) };
            var results = await Task.WhenAll(invocations);
            return results[0];
        }

        private static async Task<string> InvokeAsync(ExecutionDelegate<string> next)
        {
            await Task.Yield();
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
