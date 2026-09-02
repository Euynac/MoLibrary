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

        // A trailing separator on the resolved executable is normalized away before quoting.
        var executable = Path.Combine(Path.GetTempPath(), "Monica.Workflow.exe") + Path.DirectorySeparatorChar;

        integration.AutoStartCommand(executable)
            .Should().Be($"\"{Path.Combine(Path.GetTempPath(), "Monica.Workflow.exe")}\" serve --no-open-browser");
    }

    [Fact]
    public void ShortcutPath_UsesTheFixedFileNameUnderEachInjectedShellDirectory()
    {
        var desktopDirectory = Path.Combine(Path.GetTempPath(), "guide-shortcuts", "Desktop");
        var programsDirectory = Path.Combine(Path.GetTempPath(), "guide-shortcuts", "Programs");
        var integration = new GuideDesktopIntegration(
            "Monica Workflow",
            "Monica.Workflow",
            "serve",
            "serve --no-open-browser",
            desktopDirectory: desktopDirectory,
            programsDirectory: programsDirectory);

        integration.ShortcutPath(GuideShortcutSite.Desktop)
            .Should().Be(Path.Combine(desktopDirectory, "Monica Workflow.lnk"));
        integration.ShortcutPath(GuideShortcutSite.StartMenu)
            .Should().Be(Path.Combine(programsDirectory, "Monica Workflow.lnk"));
    }
}
