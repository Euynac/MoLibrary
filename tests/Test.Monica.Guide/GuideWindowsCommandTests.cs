using Monica.Guide;
using Xunit;

namespace Test.Monica.Guide;

public sealed class GuideWindowsCommandTests
{
    [Fact]
    public void Resolve_WhenWhereFindsNpmShim_ShouldReturnFirstExistingExactPath()
    {
        const string missing = @"C:\tools\missing\pi.cmd";
        const string shim = @"C:\Program Files\nodejs\pi.cmd";
        const string executable = @"C:\tools\pi.exe";
        var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            shim,
            executable
        };

        var resolved = GuideWindowsCommand.Resolve(
            "pi",
            command => new GuideProcessResult(
                0,
                $"{missing}\r\n{shim}\r\n{executable}\r\n",
                string.Empty),
            path: null,
            pathExtensions: ".COM;.EXE;.BAT;.CMD",
            existingPaths.Contains);

        Assert.Equal(shim, resolved);
    }

    [Fact]
    public void Resolve_WhenWhereListsExtensionlessShimFirst_ShouldPreferTheExecutableSibling()
    {
        const string posixShim = @"C:\Users\mo\AppData\Roaming\npm\pi";
        const string batchShim = @"C:\Users\mo\AppData\Roaming\npm\pi.cmd";
        var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            posixShim,
            batchShim
        };

        var resolved = GuideWindowsCommand.Resolve(
            "pi",
            command => new GuideProcessResult(
                0,
                $"{posixShim}\r\n{batchShim}\r\n",
                string.Empty),
            path: null,
            pathExtensions: ".COM;.EXE;.BAT;.CMD",
            existingPaths.Contains);

        Assert.Equal(batchShim, resolved);
    }

    [Fact]
    public void Resolve_WhenWhereOnlyFindsAnExtensionlessBinary_ShouldFallBackToIt()
    {
        const string extensionless = @"C:\Users\mo\.local\bin\claude";

        var resolved = GuideWindowsCommand.Resolve(
            "claude",
            command => new GuideProcessResult(0, $"{extensionless}\r\n", string.Empty),
            path: null,
            pathExtensions: ".COM;.EXE;.BAT;.CMD",
            candidate => candidate.Equals(extensionless, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(extensionless, resolved);
    }

    [Fact]
    public void Resolve_WhenGivenExtensionlessExplicitPath_ShouldPreferTheExecutableSibling()
    {
        const string posixShim = @"C:\Users\mo\AppData\Roaming\npm\codex";
        const string batchShim = @"C:\Users\mo\AppData\Roaming\npm\codex.cmd";
        var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            posixShim,
            batchShim
        };

        var resolved = GuideWindowsCommand.Resolve(
            posixShim,
            command => new GuideProcessResult(127, string.Empty, "where.exe was not used"),
            path: null,
            pathExtensions: ".COM;.EXE;.BAT;.CMD",
            existingPaths.Contains);

        Assert.Equal(batchShim, resolved, ignoreCase: true);
    }

    [Fact]
    public void Resolve_WhenWhereIsUnavailable_ShouldUsePathExtOrder()
    {
        const string firstDirectory = @"C:\empty";
        const string secondDirectory = @"C:\agent tools";
        var expected = Path.Combine(secondDirectory, "claude.CMD");

        var resolved = GuideWindowsCommand.Resolve(
            "claude",
            _ => new GuideProcessResult(127, string.Empty, "where.exe was not found"),
            $"{firstDirectory};\"{secondDirectory}\"",
            ".BAT;.CMD;.EXE",
            candidate => candidate.Equals(expected, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(expected, resolved, ignoreCase: true);
    }

    [Fact]
    public void TryBuildBatchCommandLine_WhenPathsContainSpaces_ShouldQuoteEveryToken()
    {
        var success = GuideWindowsCommand.TryBuildBatchCommandLine(
            @"C:\Program Files\nodejs\pi.cmd",
            ["install", @"C:\Agent Skills\monica workflow"],
            out var commandLine,
            out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(
            "\"\"C:\\Program Files\\nodejs\\pi.cmd\" \"install\" \"C:\\Agent Skills\\monica workflow\"\"",
            commandLine);
    }

    [Theory]
    [InlineData("adapter & whoami")]
    [InlineData("%COMSPEC%")]
    [InlineData("unsafe\"quote")]
    [InlineData("line\r\nbreak")]
    public void TryBuildBatchCommandLine_WhenTokenCanChangeCmdParsing_ShouldRefuseExecution(string unsafeArgument)
    {
        var success = GuideWindowsCommand.TryBuildBatchCommandLine(
            @"C:\tools\pi.cmd",
            ["install", unsafeArgument],
            out var commandLine,
            out var error);

        Assert.False(success);
        Assert.Equal(string.Empty, commandLine);
        Assert.NotNull(error);
    }
}
