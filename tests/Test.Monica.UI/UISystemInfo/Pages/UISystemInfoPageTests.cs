using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Core.Results;
using Monica.Modules;
using Monica.UI.Pages;
using Monica.UI.UISystemInfo.State;
using Xunit;

namespace Test.Monica.UI.UISystemInfo.Pages;

public sealed class UISystemInfoPageTests
{
    [Fact]
    public async Task Render_WhenLinksAreConfigured_ShouldPlaceThemBeforeTheDetailedDossier()
    {
        var snapshot = SystemInfoTestData.Snapshot();
        var factory = new SystemInfoPageSessionFactory(SystemInfoTestData.Calls(
            () => Res.Ok(snapshot)));
        var options = Options.Create(new ModuleSystemInfoUIOption
        {
            CustomLinks =
            [
                SystemInfoTestData.Link("API console", "/swagger")
            ]
        });
        await using var context = new SystemInfoUiTestContext(configureServices: services =>
        {
            services.AddSingleton(factory);
            services.AddSingleton<IOptions<ModuleSystemInfoUIOption>>(options);
        });

        var cut = context.Render<UISystemInfoPage>();

        var directChildren = cut.Find(".system-info-page").Children.ToList();
        var heroIndex = directChildren.FindIndex(static element => element.ClassList.Contains("system-info-hero"));
        var pulseIndex = directChildren.FindIndex(static element => element.ClassList.Contains("system-info-pulse"));
        var linksIndex = directChildren.FindIndex(static element => element.ClassList.Contains("system-info-links"));
        var dossierIndex = directChildren.FindIndex(static element => element.ClassList.Contains("system-info-dossier"));
        new[] { heroIndex, pulseIndex, linksIndex, dossierIndex }
            .Should().OnlyContain(index => index >= 0);
        heroIndex.Should().BeLessThan(pulseIndex);
        pulseIndex.Should().BeLessThan(linksIndex);
        linksIndex.Should().BeLessThan(dossierIndex);
    }

    [Fact]
    public async Task Retry_WhenInitialCaptureFailed_ShouldReplaceErrorWithEvidence()
    {
        var callCount = 0;
        var factory = new SystemInfoPageSessionFactory(SystemInfoTestData.Calls(
            () => ++callCount == 1
                ? Res.Fail("initial capture failed")
                : Res.Ok(SystemInfoTestData.Snapshot())));
        await using var context = CreateContext(factory, new ModuleSystemInfoUIOption());

        var cut = context.Render<UISystemInfoPage>();

        cut.Find(".system-info-load-state[role='alert']").TextContent
            .Should().Contain("initial capture failed");
        cut.FindAll("button")
            .Single(button => button.TextContent.Contains("Page:Actions:Retry", StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".system-info-load-state").Should().BeEmpty();
            cut.FindAll(".system-info-hero").Should().ContainSingle();
        });
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task Refresh_WhenCaptureFails_ShouldKeepExistingEvidenceVisible()
    {
        var snapshot = SystemInfoTestData.Snapshot();
        var callCount = 0;
        var factory = new SystemInfoPageSessionFactory(SystemInfoTestData.Calls(
            () => ++callCount == 1 ? Res.Ok(snapshot) : Res.Fail("refresh failed")));
        await using var context = CreateContext(factory, new ModuleSystemInfoUIOption());

        var cut = context.Render<UISystemInfoPage>();
        var originalIdentity = cut.Find(".system-info-hero__name h2").TextContent;

        cut.FindAll("button")
            .Single(button => button.TextContent.Contains("Page:Actions:Refresh", StringComparison.Ordinal))
            .Click();

        cut.Find(".system-info-hero__name h2").TextContent.Should().Be(originalIdentity);
        cut.FindAll(".system-info-load-state").Should().BeEmpty();
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task Render_WhenEveryConfiguredLinkIsDisabled_ShouldOmitTheLinkPanel()
    {
        var factory = new SystemInfoPageSessionFactory(SystemInfoTestData.Calls(
            () => Res.Ok(SystemInfoTestData.Snapshot())));
        var options = new ModuleSystemInfoUIOption
        {
            CustomLinks =
            [
                SystemInfoTestData.Link("Disabled", "/disabled", enabled: false)
            ]
        };
        await using var context = CreateContext(factory, options);

        var cut = context.Render<UISystemInfoPage>();

        cut.FindAll(".system-info-links").Should().BeEmpty();
        cut.FindAll(".system-info-dossier").Should().ContainSingle();
    }

    private static SystemInfoUiTestContext CreateContext(
        SystemInfoPageSessionFactory factory,
        ModuleSystemInfoUIOption options) => new(configureServices: services =>
    {
        services.AddSingleton(factory);
        services.AddSingleton<IOptions<ModuleSystemInfoUIOption>>(Options.Create(options));
    });
}
