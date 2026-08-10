using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Monica.UI.UISystemInfo.Components;
using Xunit;

namespace Test.Monica.UI.UISystemInfo.Components;

public sealed class SystemInfoDossierTests
{
    [Fact]
    public async Task SectionNavigation_ShouldScrollWithoutChangingTheUrlFragment()
    {
        await using var context = new SystemInfoUiTestContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/system-info");
        var initialUri = navigation.Uri;
        var cut = context.Render<SystemInfoDossier>(parameters => parameters
            .Add(component => component.Snapshot, SystemInfoTestData.Snapshot()));

        var navigationItems = cut.FindAll(
            ".system-info-dossier__desktop-nav .system-info-dossier__nav-item");
        navigationItems.Should().HaveCount(4);
        navigationItems.Should().OnlyContain(item =>
            item.TagName.Equals("BUTTON", StringComparison.OrdinalIgnoreCase)
            && !item.HasAttribute("href"));

        await navigationItems[1].ClickAsync();

        navigation.Uri.Should().Be(initialUri);
        cut.FindAll(".system-info-dossier__nav-item[aria-current='location']")
            .Should().ContainSingle()
            .Which.TextContent.Should().Contain("Page:Sections:Environment:Title");
        var scrollInvocations = context.JSInterop.Invocations["mudScrollManager.scrollIntoView"];
        scrollInvocations.Should().ContainSingle();
        scrollInvocations[0].Arguments.Should().BeEquivalentTo(
            new object?[] { "#environment-section", "auto" });
    }
}
