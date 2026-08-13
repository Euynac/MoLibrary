using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Monica.Testing.Localization;
using Monica.UI.Localization;
using Monica.UI.Pages;
using MudBlazor.Services;
using Xunit;

namespace Test.Monica.UI;

public sealed class OperationalStatePageTests
{
    [Fact]
    public void ErrorPage_ShouldRenderLocalizedResponsiveRecoveryState()
    {
        using var context = CreateContext();

        var cut = context.Render<ErrorPage>();

        cut.Find(".error-page__surface").GetAttribute("role").Should().Be("alert");
        cut.Find("#error-page-title").TextContent.Should().Be("Error:Title");
        cut.Markup.Should().Contain("Error:BackToOverview");
        cut.Markup.Should().Contain("Error:DevelopmentModeInstruction");
        cut.Markup.Should().NotContain("ASPNETCORE_ENVIRONMENT");
    }

    [Fact]
    public void NotFoundPage_ShouldRemainAMinimalLocalizedState()
    {
        using var context = CreateContext();

        var cut = context.Render<NotFoundPage>();

        cut.FindAll(".mud-alert").Should().ContainSingle();
        cut.Markup.Should().Contain("PageNotFound:Title");
        cut.Markup.Should().Contain("PageNotFound:Message");
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IStringLocalizer<SharedResource>, EchoStringLocalizer<SharedResource>>();
        return context;
    }
}
