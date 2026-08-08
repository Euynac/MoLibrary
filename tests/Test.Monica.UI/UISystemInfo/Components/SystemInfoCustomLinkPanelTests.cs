using AwesomeAssertions;
using Bunit;
using Monica.UI.UISystemInfo.Components;
using Monica.UI.UISystemInfo.Models;
using Xunit;

namespace Test.Monica.UI.UISystemInfo.Components;

public sealed class SystemInfoCustomLinkPanelTests
{
    [Fact]
    public async Task Render_ShouldOrderEnabledLinksSecureNewTabsAndKeepCredentialsOutOfMarkup()
    {
        await using var context = new SystemInfoUiTestContext();
        var clipboard = context.JSInterop
            .Setup<bool>(invocation => invocation.Identifier == "MoClipboard.copyText")
            .SetResult(true);
        var links = new[]
        {
            SystemInfoTestData.Link("Later", "/later", order: 20),
            SystemInfoTestData.Link(
                "Secure console",
                "https://localhost:7093/console",
                order: 10,
                target: SystemInfoCustomLinkTarget.NewTab,
                userName: "diagnostic-user",
                password: "diagnostic-secret"),
            SystemInfoTestData.Link("Disabled", "/disabled", order: 0, enabled: false)
        };

        var cut = context.Render<SystemInfoCustomLinkPanel>(parameters => parameters
            .Add(component => component.Links, links));

        var renderedLinks = cut.FindAll(".system-info-link");
        renderedLinks.Should().HaveCount(2);
        renderedLinks[0].TextContent.Should().Contain("Secure console");
        renderedLinks[1].TextContent.Should().Contain("Later");
        cut.Markup.Should().NotContain("Disabled");
        cut.Markup.Should().NotContain("diagnostic-user");
        cut.Markup.Should().NotContain("diagnostic-secret");
        cut.Markup.Should().Contain("CustomLinks:Labels:Access");

        var secureAnchor = renderedLinks[0].QuerySelector("a");
        secureAnchor.Should().NotBeNull();
        secureAnchor!.GetAttribute("target").Should().Be("_blank");
        secureAnchor.GetAttribute("rel")!
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Should().BeEquivalentTo("noopener", "noreferrer");

        await OpenAccessAsync();
        var userNameItem = context.PopoverProvider
            .FindAll(".mud-popover-open .mud-menu-item")
            .Single(item => item.TextContent.Contains(
                "CustomLinks:Actions:CopyUserName",
                StringComparison.Ordinal));
        await userNameItem.ClickAsync();
        await context.PopoverProvider.WaitForAssertionAsync(() =>
            context.PopoverProvider.FindAll(".mud-popover-open").Should().BeEmpty());

        await OpenAccessAsync();
        var passwordItem = context.PopoverProvider
            .FindAll(".mud-popover-open .mud-menu-item")
            .Single(item => item.TextContent.Contains(
                "CustomLinks:Actions:CopyPassword",
                StringComparison.Ordinal));
        await passwordItem.ClickAsync();

        clipboard.VerifyInvoke("MoClipboard.copyText", calledTimes: 2);
        var invocations = context.JSInterop.Invocations["MoClipboard.copyText"];
        invocations[0].Arguments.Should().BeEquivalentTo(new object?[] { "diagnostic-user" });
        invocations[1].Arguments.Should().BeEquivalentTo(new object?[] { "diagnostic-secret" });

        async Task OpenAccessAsync()
        {
            await cut.Find(".system-info-link__access button.mud-button-root").ClickAsync();
            await context.PopoverProvider.WaitForAssertionAsync(() =>
                context.PopoverProvider.FindAll(".mud-popover-open .mud-menu-item")
                    .Should().HaveCount(2));
            context.PopoverProvider.Markup.Should().NotContain("diagnostic-user");
            context.PopoverProvider.Markup.Should().NotContain("diagnostic-secret");
        }
    }
}
