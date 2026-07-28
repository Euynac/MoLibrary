using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Execution;
using Monica.Core.Mediator;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Mediator;

public sealed class MediatorExecutionPipelineTests
{
    [Fact]
    public async Task Send_ShouldExecuteHandlerThroughMediatorPointInTheSameScope()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddScoped<ScopeIdentity>();
        builder.Services.AddTransient<IRequestHandler<TestRequest, string>, TestHandler>();
        builder.AddMonica(monica =>
        {
            monica.AddMediator();
            monica.AddExecutionPipeline().AddBehavior<MediatorBehavior>(
                descriptorFilter: descriptor => descriptor.Point == MediatorExecutionPoints.Request,
                lifetime: ServiceLifetime.Scoped);
        });

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        var expectedIdentity = scope.ServiceProvider.GetRequiredService<ScopeIdentity>().Value;

        var response = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new TestRequest("input"), TestContext.Current.CancellationToken);

        response.Should().Be($"{expectedIdentity}:input");
    }

    [Fact]
    public async Task Send_WhenExecutionBehaviorShortCircuits_ShouldNotInvokeHandler()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<HandlerInvocationCounter>();
        builder.Services.AddTransient<IRequestHandler<ShortCircuitRequest, string>, ShortCircuitHandler>();
        builder.AddMonica(monica =>
        {
            monica.AddMediator();
            monica.AddExecutionPipeline().AddBehavior<ShortCircuitBehavior>(
                descriptorFilter: descriptor => descriptor.Point == MediatorExecutionPoints.Request);
        });

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        var response = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new ShortCircuitRequest(), TestContext.Current.CancellationToken);

        response.Should().Be("short-circuited");
        host.Services.GetRequiredService<HandlerInvocationCounter>().Value.Should().Be(0);
    }

    private sealed record TestRequest(string Value) : IRequest<string>;

    private sealed record ShortCircuitRequest : IRequest<string>;

    private sealed class ScopeIdentity
    {
        public Guid Value { get; } = Guid.NewGuid();
    }

    private sealed class HandlerInvocationCounter
    {
        public int Value;
    }

    private sealed class TestHandler(ScopeIdentity identity) : IRequestHandler<TestRequest, string>
    {
        public Task<string> Handle(TestRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult($"{identity.Value}:{request.Value}");
        }
    }

    private sealed class ShortCircuitHandler(HandlerInvocationCounter counter)
        : IRequestHandler<ShortCircuitRequest, string>
    {
        public Task<string> Handle(ShortCircuitRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref counter.Value);
            return Task.FromResult("handler");
        }
    }

    private sealed class MediatorBehavior(ScopeIdentity identity)
        : IExecutionBehavior<TestRequest, string>
    {
        public async Task<string> ExecuteAsync(
            ExecutionContext<TestRequest> context,
            ExecutionDelegate<string> next)
        {
            context.Descriptor.Point.Should().Be(MediatorExecutionPoints.Request);
            context.Descriptor.TransactionMode.Should().Be(ExecutionTransactionMode.Automatic);
            context.Target.Should().BeOfType<TestHandler>();
            var result = await next();
            result.Should().StartWith(identity.Value.ToString());
            return result;
        }
    }

    private sealed class ShortCircuitBehavior
        : IExecutionBehavior<ShortCircuitRequest, string>
    {
        public Task<string> ExecuteAsync(
            ExecutionContext<ShortCircuitRequest> context,
            ExecutionDelegate<string> next)
        {
            return Task.FromResult("short-circuited");
        }
    }
}
