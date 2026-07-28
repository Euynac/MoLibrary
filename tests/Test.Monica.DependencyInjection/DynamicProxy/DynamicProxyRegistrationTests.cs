using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Execution;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Extensions;
using Monica.DependencyInjection.DynamicProxy.Abstractions;
using Monica.DependencyInjection.DynamicProxy.Models;
using Monica.DependencyInjection.DynamicProxy.Utils;
using Monica.Modules;
using Xunit;

namespace Test.Monica.DependencyInjection.DynamicProxy;

public sealed class DynamicProxyRegistrationTests
{
    [Fact]
    public async Task UseExecutionPipeline_WhenSelectedServiceReturnsTasks_ShouldExecuteTaskMethodsThroughPipeline()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ExecutionTracker>();
        builder.Services.AddTransient<IBridgeService, BridgeService>();
        builder.AddMonica(monica =>
        {
            monica.AddExecutionPipeline()
                .AddBehavior(
                    typeof(TrackingExecutionBehavior<,>),
                    descriptorFilter: descriptor => descriptor.Point == DynamicProxyExecutionPoints.Method);
            monica.AddDynamicProxy()
                .UseExecutionPipeline(context => context.ServiceType == typeof(IBridgeService));
        });
        using var host = builder.Build();

        var service = host.Services.GetRequiredService<IBridgeService>();
        await service.ExecuteAsync();
        var value = await service.GetValueAsync();
        var synchronousValue = service.GetSynchronousValue();
        var valueTaskValue = await service.GetValueTaskAsync();

        value.Should().Be("bridged");
        synchronousValue.Should().Be(42);
        valueTaskValue.Should().Be(84);
        host.Services.GetRequiredService<ExecutionTracker>().ResultTypes
            .Should().Equal(typeof(ExecutionUnit), typeof(string));
    }

    [Fact]
    public async Task UseExecutionPipeline_WhenScopedServiceIsResolved_ShouldUseInvocationScope()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ExecutionScopeTracker>();
        builder.Services.AddScoped<ExecutionScopeMarker>();
        builder.Services.AddScoped<IBridgeService, BridgeService>();
        builder.AddMonica(monica =>
        {
            monica.AddExecutionPipeline()
                .AddBehavior(
                    typeof(ScopeTrackingExecutionBehavior<,>),
                    descriptorFilter: descriptor => descriptor.Point == DynamicProxyExecutionPoints.Method);
            monica.AddDynamicProxy()
                .UseExecutionPipeline(context => context.ServiceType == typeof(IBridgeService));
        });
        using var host = builder.Build();

        using (var firstScope = host.Services.CreateScope())
        {
            await firstScope.ServiceProvider.GetRequiredService<IBridgeService>().ExecuteAsync();
        }

        using (var secondScope = host.Services.CreateScope())
        {
            await secondScope.ServiceProvider.GetRequiredService<IBridgeService>().ExecuteAsync();
        }

        host.Services.GetRequiredService<ExecutionScopeTracker>().ScopeIds
            .Should().OnlyHaveUniqueItems().And.HaveCount(2);
    }

    [Fact]
    public async Task UseExecutionPipeline_WhenMethodIsInherited_ShouldUseRegisteredImplementationIdentity()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ComponentIdentityTracker>();
        builder.Services.AddTransient<IInheritedBridgeService, DerivedInheritedBridgeService>();
        builder.AddMonica(monica =>
        {
            monica.AddExecutionPipeline()
                .AddBehavior(
                    typeof(ComponentIdentityExecutionBehavior<,>),
                    descriptorFilter: descriptor =>
                        descriptor.Point == DynamicProxyExecutionPoints.Method
                        && descriptor.ComponentType == typeof(DerivedInheritedBridgeService));
            monica.AddDynamicProxy()
                .UseExecutionPipeline(context => context.ServiceType == typeof(IInheritedBridgeService));
        });
        using var host = builder.Build();

        await host.Services.GetRequiredService<IInheritedBridgeService>().ExecuteAsync();

        var observation = host.Services.GetRequiredService<ComponentIdentityTracker>();
        observation.ComponentType.Should().Be(typeof(DerivedInheritedBridgeService));
        observation.MethodDeclaringType.Should().Be(typeof(InheritedBridgeServiceBase));
        observation.HasDerivedMetadata.Should().BeTrue();
    }

    [Fact]
    public async Task UseExecutionPipeline_WhenFactoryReturnsDerivedTarget_ShouldUseConcreteTargetIdentity()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ComponentIdentityTracker>();
        builder.Services.AddTransient<IInheritedBridgeService>(_ => new DerivedInheritedBridgeService());
        builder.AddMonica(monica =>
        {
            monica.AddExecutionPipeline()
                .AddBehavior(
                    typeof(ComponentIdentityExecutionBehavior<,>),
                    descriptorFilter: descriptor =>
                        descriptor.Point == DynamicProxyExecutionPoints.Method
                        && descriptor.ComponentType == typeof(DerivedInheritedBridgeService));
            monica.AddDynamicProxy()
                .UseExecutionPipeline(context => context.ServiceType == typeof(IInheritedBridgeService));
        });
        using var host = builder.Build();

        await host.Services.GetRequiredService<IInheritedBridgeService>().ExecuteAsync();

        var observation = host.Services.GetRequiredService<ComponentIdentityTracker>();
        observation.ComponentType.Should().Be(typeof(DerivedInheritedBridgeService));
        observation.MethodDeclaringType.Should().Be(typeof(InheritedBridgeServiceBase));
        observation.HasDerivedMetadata.Should().BeTrue();
    }

    [Fact]
    public void UseExecutionPipeline_WhenPredicateSelectsSingleton_ShouldRejectComposition()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IBridgeService, BridgeService>();

        Action compose = () => builder.AddMonica(monica =>
            monica.AddDynamicProxy()
                .UseExecutionPipeline(context => context.ServiceType == typeof(IBridgeService)));

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage($"*{nameof(IBridgeService)}*")
            .WithMessage("*singleton*")
            .WithMessage("*invocation scope*");
    }

    [Fact]
    public async Task UseExecutionPipeline_WhenCustomInterceptorAlsoMatches_ShouldComposeBothInterceptors()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ExecutionTracker>();
        builder.Services.AddSingleton<InvocationTracker>();
        builder.Services.AddTransient<IBridgeService, BridgeService>();
        builder.AddMonica(monica =>
        {
            monica.AddExecutionPipeline()
                .AddBehavior(
                    typeof(TrackingExecutionBehavior<,>),
                    descriptorFilter: descriptor => descriptor.Point == DynamicProxyExecutionPoints.Method);
            monica.AddDynamicProxy()
                .UseExecutionPipeline(context => context.ServiceType == typeof(IBridgeService))
                .AddInterceptor<TrackingInterceptor>(
                    context => context.ServiceType == typeof(IBridgeService));
        });
        using var host = builder.Build();

        await host.Services.GetRequiredService<IBridgeService>().ExecuteAsync();

        host.Services.GetRequiredService<ExecutionTracker>().ResultTypes.Should().ContainSingle()
            .Which.Should().Be(typeof(ExecutionUnit));
        host.Services.GetRequiredService<InvocationTracker>().InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task UseExecutionPipeline_WhenComponentHasModuleOwnedAdapter_ShouldExcludeProxyBridge()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ExecutionTracker>();
        builder.Services.AddTransient<IAdapterOwnedService, AdapterOwnedService>();
        builder.AddMonica(monica =>
        {
            monica.AddExecutionPipeline()
                .AddBehavior(
                    typeof(TrackingExecutionBehavior<,>),
                    descriptorFilter: descriptor => descriptor.Point == DynamicProxyExecutionPoints.Method);
            monica.AddDynamicProxy()
                .UseExecutionPipeline(context => context.ServiceType == typeof(IAdapterOwnedService));
        });
        using var host = builder.Build();

        var service = host.Services.GetRequiredService<IAdapterOwnedService>();
        await service.ExecuteAsync();

        ProxyHelper.IsProxy(service).Should().BeFalse();
        host.Services.GetRequiredService<ExecutionTracker>().ResultTypes.Should().BeEmpty();
    }

    [Fact]
    public void AddMonica_WhenSealedConcreteServiceMatchesInterceptor_ShouldRejectComposition()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddTransient<SealedConcreteService>();

        Action compose = () => builder.AddMonica(monica =>
            monica.AddDynamicProxy()
                .AddInterceptor<TrackingInterceptor>(ShouldInterceptTestService));

        compose.Should().Throw<ModuleRegistrationException>()
            .WithMessage($"*{nameof(SealedConcreteService)}*")
            .WithMessage("*ClassProxy*")
            .WithMessage("*sealed*")
            .WithMessage($"*{nameof(TrackingInterceptor)}*")
            .WithMessage("*non-sealed*")
            .WithMessage("*InterfaceProxy*");
    }

    [Fact]
    public async Task Resolve_WhenSealedImplementationUsesInterfaceProxy_ShouldInterceptInvocation()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<InvocationTracker>();
        builder.Services.AddTransient<ITestService, SealedInterfaceService>();
        builder.AddMonica(monica =>
            monica.AddDynamicProxy()
                .AddInterceptor<TrackingInterceptor>(ShouldInterceptTestService));
        using var host = builder.Build();

        var service = host.Services.GetRequiredService<ITestService>();
        await service.ExecuteAsync();

        ProxyHelper.IsProxy(service).Should().BeTrue();
        var tracker = host.Services.GetRequiredService<InvocationTracker>();
        tracker.InvocationCount.Should().Be(1);
        tracker.LastTarget.Should().BeOfType<SealedInterfaceService>();
    }

    [Fact]
    public async Task Resolve_WhenNonSealedConcreteServiceUsesClassProxy_ShouldInterceptVirtualInvocation()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<InvocationTracker>();
        builder.Services.AddTransient<InheritableConcreteService>();
        builder.AddMonica(monica =>
            monica.AddDynamicProxy()
                .AddInterceptor<TrackingInterceptor>(ShouldInterceptTestService));
        using var host = builder.Build();

        var service = host.Services.GetRequiredService<InheritableConcreteService>();
        await service.ExecuteAsync();

        ProxyHelper.IsProxy(service).Should().BeTrue();
        var tracker = host.Services.GetRequiredService<InvocationTracker>();
        tracker.InvocationCount.Should().Be(1);
        tracker.LastTarget.Should().BeAssignableTo<InheritableConcreteService>();
    }

    private static bool ShouldInterceptTestService(ProxyBuildContext context)
    {
        return context.ImplementationType == typeof(SealedConcreteService)
               || context.ImplementationType == typeof(SealedInterfaceService)
               || context.ImplementationType == typeof(InheritableConcreteService);
    }

    public interface ITestService
    {
        Task ExecuteAsync();
    }

    public interface IBridgeService
    {
        Task ExecuteAsync();

        Task<string> GetValueAsync();

        int GetSynchronousValue();

        ValueTask<int> GetValueTaskAsync();
    }

    public interface IAdapterOwnedService : IExecutionAdapterOwnedComponent
    {
        Task ExecuteAsync();
    }

    public interface IInheritedBridgeService
    {
        Task ExecuteAsync();
    }

    public sealed class BridgeService : IBridgeService
    {
        public Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }

        public Task<string> GetValueAsync()
        {
            return Task.FromResult("bridged");
        }

        public int GetSynchronousValue()
        {
            return 42;
        }

        public ValueTask<int> GetValueTaskAsync()
        {
            return ValueTask.FromResult(84);
        }
    }

    public sealed class AdapterOwnedService : IAdapterOwnedService
    {
        public Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }

    public abstract class InheritedBridgeServiceBase : IInheritedBridgeService
    {
        public Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }

    [DerivedExecutionMetadata]
    public sealed class DerivedInheritedBridgeService : InheritedBridgeServiceBase;

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class DerivedExecutionMetadataAttribute : Attribute;

    public sealed class SealedConcreteService
    {
        public Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }

    public sealed class SealedInterfaceService : ITestService
    {
        public Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }

    public class InheritableConcreteService
    {
        public virtual Task ExecuteAsync()
        {
            return Task.CompletedTask;
        }
    }

    public sealed class InvocationTracker
    {
        public int InvocationCount { get; private set; }

        public object? LastTarget { get; private set; }

        public void RecordInvocation(object target)
        {
            InvocationCount++;
            LastTarget = target;
        }
    }

    public sealed class ExecutionTracker
    {
        public List<Type> ResultTypes { get; } = [];

        public void Record(Type resultType)
        {
            ResultTypes.Add(resultType);
        }
    }

    public sealed class ExecutionScopeMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public sealed class ExecutionScopeTracker
    {
        public List<Guid> ScopeIds { get; } = [];

        public void Record(Guid scopeId)
        {
            ScopeIds.Add(scopeId);
        }
    }

    public sealed class ComponentIdentityTracker
    {
        public Type? ComponentType { get; private set; }

        public Type? MethodDeclaringType { get; private set; }

        public bool HasDerivedMetadata { get; private set; }

        public void Record(ExecutionDescriptor descriptor)
        {
            ComponentType = descriptor.ComponentType;
            MethodDeclaringType = descriptor.EntryMethod?.DeclaringType;
            HasDerivedMetadata = descriptor.ComponentType.IsDefined(
                typeof(DerivedExecutionMetadataAttribute),
                inherit: false);
        }
    }

    public sealed class TrackingExecutionBehavior<TInput, TResult>(ExecutionTracker tracker)
        : IExecutionBehavior<TInput, TResult>
    {
        public async Task<TResult> ExecuteAsync(
            ExecutionContext<TInput> context,
            ExecutionDelegate<TResult> next)
        {
            context.Descriptor.TransactionMode.Should().Be(ExecutionTransactionMode.Automatic);
            tracker.Record(typeof(TResult));
            return await next();
        }
    }

    public sealed class ScopeTrackingExecutionBehavior<TInput, TResult>(
        ExecutionScopeTracker tracker,
        ExecutionScopeMarker marker)
        : IExecutionBehavior<TInput, TResult>
    {
        public async Task<TResult> ExecuteAsync(
            ExecutionContext<TInput> context,
            ExecutionDelegate<TResult> next)
        {
            tracker.Record(marker.Id);
            return await next();
        }
    }

    public sealed class ComponentIdentityExecutionBehavior<TInput, TResult>(ComponentIdentityTracker tracker)
        : IExecutionBehavior<TInput, TResult>
    {
        public async Task<TResult> ExecuteAsync(
            ExecutionContext<TInput> context,
            ExecutionDelegate<TResult> next)
        {
            tracker.Record(context.Descriptor);
            return await next();
        }
    }

    public sealed class TrackingInterceptor(InvocationTracker tracker) : InvocationInterceptor
    {
        public override async Task InterceptAsync(IMethodInvocation invocation)
        {
            tracker.RecordInvocation(invocation.TargetObject);
            await invocation.ProceedAsync();
        }
    }
}
