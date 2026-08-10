using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Monica.UI.Shell.Support;
using Xunit;

namespace Test.Monica.UI.Shell.Support;

public sealed class PageAccessEvaluatorTests
{
    [Fact]
    public async Task IsAuthorizedAsync_WhenPageHasNoPolicy_ShouldAllowAccess()
    {
        var registry = new PageRegistry();
        registry.RegisterPage<TestPage>("open", "Open");
        registry.Seal();
        using var services = new ServiceCollection().BuildServiceProvider();
        var evaluator = new PageAccessEvaluator(registry, services);

        var authorized = await evaluator.IsAuthorizedAsync(
            "/OPEN/",
            TestContext.Current.CancellationToken);

        authorized.Should().BeTrue();
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenRegisteredPolicyDenies_ShouldDenyNavigationAndRoute()
    {
        var registry = new PageRegistry();
        registry.RegisterPage<TestPage>(
            "protected",
            "Protected",
            addToNav: true,
            accessPolicyType: typeof(DenyPolicy));
        registry.Seal();
        await using var services = new ServiceCollection()
            .AddSingleton(new DenyPolicy())
            .BuildServiceProvider();
        var evaluator = new PageAccessEvaluator(registry, services);

        (await evaluator.IsAuthorizedAsync("protected", TestContext.Current.CancellationToken))
            .Should().BeFalse();
        (await evaluator.IsAuthorizedAsync(
                registry.GetNavItems().Single(),
                TestContext.Current.CancellationToken))
            .Should().BeFalse();
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenPolicyRegistrationIsMissingOrFails_ShouldFailClosed()
    {
        var missingRegistry = CreateProtectedRegistry<AllowPolicy>();
        using var missingServices = new ServiceCollection().BuildServiceProvider();
        var missingEvaluator = new PageAccessEvaluator(missingRegistry, missingServices);

        (await missingEvaluator.IsAuthorizedAsync("protected", TestContext.Current.CancellationToken))
            .Should().BeFalse();

        var throwingRegistry = CreateProtectedRegistry<ThrowingPolicy>();
        await using var throwingServices = new ServiceCollection()
            .AddSingleton(new ThrowingPolicy())
            .BuildServiceProvider();
        var throwingEvaluator = new PageAccessEvaluator(throwingRegistry, throwingServices);

        (await throwingEvaluator.IsAuthorizedAsync("protected", TestContext.Current.CancellationToken))
            .Should().BeFalse();

        var activationRegistry = CreateProtectedRegistry<ThrowingConstructorPolicy>();
        await using var activationServices = new ServiceCollection()
            .AddSingleton<ThrowingConstructorPolicy>()
            .BuildServiceProvider();
        var activationEvaluator = new PageAccessEvaluator(activationRegistry, activationServices);

        (await activationEvaluator.IsAuthorizedAsync("protected", TestContext.Current.CancellationToken))
            .Should().BeFalse();
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenCancellationIsRequested_ShouldPropagateCancellation()
    {
        var registry = CreateProtectedRegistry<AllowPolicy>();
        await using var services = new ServiceCollection()
            .AddSingleton(new AllowPolicy())
            .BuildServiceProvider();
        var evaluator = new PageAccessEvaluator(registry, services);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = () => evaluator.IsAuthorizedAsync("protected", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static PageRegistry CreateProtectedRegistry<TPolicy>()
        where TPolicy : class, IPageAccessPolicy
    {
        var registry = new PageRegistry();
        registry.RegisterPage<TestPage>("protected", "Protected", accessPolicyType: typeof(TPolicy));
        registry.Seal();
        return registry;
    }

    private sealed class TestPage : ComponentBase;

    private sealed class AllowPolicy : IPageAccessPolicy
    {
        public Task<bool> IsAuthorizedAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }
    }

    private sealed class DenyPolicy : IPageAccessPolicy
    {
        public Task<bool> IsAuthorizedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class ThrowingPolicy : IPageAccessPolicy
    {
        public Task<bool> IsAuthorizedAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Policy failure");
        }
    }

    private sealed class ThrowingConstructorPolicy : IPageAccessPolicy
    {
        public ThrowingConstructorPolicy()
        {
            throw new InvalidOperationException("Policy activation failure");
        }

        public Task<bool> IsAuthorizedAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
