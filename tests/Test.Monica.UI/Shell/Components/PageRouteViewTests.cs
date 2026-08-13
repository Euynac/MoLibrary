using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Monica.Testing.Localization;
using Monica.UI.Localization;
using Monica.UI.Shell.Components;
using Monica.UI.Shell.Support;
using MudBlazor.Services;
using Xunit;

namespace Test.Monica.UI.Shell.Components;

public sealed class PageRouteViewTests
{
    [Fact]
    public void ProtectedRoute_WhenAuthorizationIsPendingOrDenied_ShouldNotInstantiatePage()
    {
        using var context = CreateContext(out var policy, out var tracker);
        var cut = RenderRoute<ProtectedTestPage>(context);

        cut.Markup.Should().Contain("RouteAccess:Checking");
        tracker.ProtectedPageInitializations.Should().Be(0);

        policy.Complete(false);

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("RouteAccess:DeniedTitle"));
        tracker.ProtectedPageInitializations.Should().Be(0);
    }

    [Fact]
    public void ProtectedRoute_WhenAuthorizationAllows_ShouldInstantiatePageAfterDecision()
    {
        using var context = CreateContext(out var policy, out var tracker);
        var cut = RenderRoute<ProtectedTestPage>(context);
        tracker.ProtectedPageInitializations.Should().Be(0);

        policy.Complete(true);

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("protected-page"));
        tracker.ProtectedPageInitializations.Should().Be(1);
    }

    [Fact]
    public void NavigationChange_ShouldAllowUnregisteredPageAndIgnoreStaleProtectedDecision()
    {
        using var context = CreateContext(out var policy, out var tracker);
        var cut = RenderRoute<ProtectedTestPage>(context);

        cut.Render(parameters => parameters
            .Add(component => component.RouteData, CreateRouteData<PublicTestPage>())
            .Add(component => component.DefaultLayout, typeof(TestLayout)));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("public-page"));
        policy.Complete(true);

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("public-page");
            tracker.ProtectedPageInitializations.Should().Be(0);
        });
    }

    private static BunitContext CreateContext(
        out ControllablePolicy policy,
        out RouteInstantiationTracker tracker)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton<IStringLocalizer<SharedResource>>(new EchoStringLocalizer<SharedResource>());

        policy = new ControllablePolicy();
        tracker = new RouteInstantiationTracker();
        var registry = new PageRegistry();
        registry.RegisterPage<ProtectedTestPage>(
            "protected",
            "Protected",
            accessPolicyType: typeof(ControllablePolicy));
        registry.Seal();

        context.Services.AddSingleton<IPageCatalog>(registry);
        context.Services.AddSingleton(policy);
        context.Services.AddSingleton(tracker);
        context.Services.AddScoped<PageAccessEvaluator>();
        return context;
    }

    private static IRenderedComponent<PageRouteView> RenderRoute<TPage>(BunitContext context)
        where TPage : ComponentBase
    {
        return context.Render<PageRouteView>(parameters => parameters
            .Add(component => component.RouteData, CreateRouteData<TPage>())
            .Add(component => component.DefaultLayout, typeof(TestLayout)));
    }

    private static RouteData CreateRouteData<TPage>()
        where TPage : ComponentBase => new(typeof(TPage), new Dictionary<string, object?>());

    public sealed class ProtectedTestPage : ComponentBase
    {
        [Inject]
        public RouteInstantiationTracker Tracker { get; set; } = null!;

        protected override void OnInitialized()
        {
            Tracker.ProtectedPageInitializations++;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, "protected-page");
        }
    }

    public sealed class PublicTestPage : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, "public-page");
        }
    }

    public sealed class TestLayout : LayoutComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, Body);
        }
    }

    public sealed class RouteInstantiationTracker
    {
        public int ProtectedPageInitializations { get; set; }
    }

    private sealed class ControllablePolicy : IPageAccessPolicy
    {
        private readonly TaskCompletionSource<bool> _decision =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> IsAuthorizedAsync(CancellationToken cancellationToken = default) =>
            _decision.Task.WaitAsync(cancellationToken);

        public void Complete(bool isAuthorized) => _decision.TrySetResult(isAuthorized);
    }
}
