using AwesomeAssertions;
using Monica.Guide.Setup;
using Xunit;

namespace Test.Monica.Guide;

public sealed class GuideDesktopIntegrationTests
{
    [Fact]
    public void AutoStartCommand_QuotesTheResolvedExecutableAndAddsTheProductArguments()
    {
        var integration = new GuideDesktopIntegration(
            "Monica Workflow",
            "Monica.Workflow",
            "serve",
            "serve --no-open-browser");

        integration.AutoStartCommand(@"C:\Tools\bundle\app\Monica.Workflow.exe\")
            .Should().Be("\"C:\\Tools\\bundle\\app\\Monica.Workflow.exe\" serve --no-open-browser");
    }

    [Fact]
    public void ShortcutPath_UsesTheFixedFileNameUnderEachInjectedShellDirectory()
    {
        var integration = new GuideDesktopIntegration(
            "Monica Workflow",
            "Monica.Workflow",
            "serve",
            "serve --no-open-browser",
            desktopDirectory: @"C:\Users\mo\Desktop",
            programsDirectory: @"C:\Users\mo\AppData\Roaming\Microsoft\Windows\Start Menu\Programs");

        integration.ShortcutPath(GuideShortcutSite.Desktop)
            .Should().Be(@"C:\Users\mo\Desktop\Monica Workflow.lnk");
        integration.ShortcutPath(GuideShortcutSite.StartMenu)
            .Should().Be(@"C:\Users\mo\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Monica Workflow.lnk");
    }
}
