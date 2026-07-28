using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        host.Services.GetRequiredService<InvocationTracker>().InvocationCount.Should().Be(1);
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
        host.Services.GetRequiredService<InvocationTracker>().InvocationCount.Should().Be(1);
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

        public void RecordInvocation()
        {
            InvocationCount++;
        }
    }

    public sealed class TrackingInterceptor(InvocationTracker tracker) : InvocationInterceptor
    {
        public override async Task InterceptAsync(IMethodInvocation invocation)
        {
            tracker.RecordInvocation();
            await invocation.ProceedAsync();
        }
    }
}
