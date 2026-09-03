using System.Text.Json;
using Monica.Guide;
using Xunit;

namespace Test.Monica.Guide;

public sealed class GuideCommandTests
{
    private static AgentProductDefinition Product => KnownAgentProducts.MonicaWorkflow;

    [Fact]
    public void Parser_AcceptsRepeatedSelectorsProfilesSkillsAndApplyDigest()
    {
        var command = GuideCommandParser.Parse([
            "configure",
            "--target", "shared",
            "--target", "claude",
            "--environment", "windows",
            "--environment", "wsl:Ubuntu:alice",
            "--skill", "monica-workflow-guide",
            "--port", "62000",
            "--apply",
            "--plan-digest", new string('a', 64),
            "--json"
        ]);

        Assert.Equal("configure", command.Name);
        Assert.Equal([GuideTarget.Shared, GuideTarget.Claude], command.Targets);
        Assert.Equal(["windows", "wsl:Ubuntu:alice"], command.Environments.Select(static item => item.Selector));
        Assert.Equal(["monica-workflow-guide"], command.Skills);
        Assert.Equal(62000, command.Port);
        Assert.True(command.Apply);
        Assert.True(command.Json);
    }

    [Fact]
    public void Parser_RejectsInvalidScopeAndUnsafeEnvironmentSelectors()
    {
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["status", "--locale", "zh-CN"]));
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["configure", "--environment", "solaris"]));
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["configure", "--apply"]));
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["overview", "--environment", "windows"]));
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["configure", "--target", "pi"]));
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["configure", "--target", "claude"]));
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["status", "--skill", "monica-guide"]));
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["status", "--profile", "application"]));
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["configure", "--skill", "a", "--profile", "b"]));
    }

    [Fact]
    public void Parser_AcceptsNativePosixEnvironmentSelectors()
    {
        Assert.Equal("linux", GuideCommandParser.ParseEnvironment("linux").Kind);
        Assert.Equal("macos", GuideCommandParser.ParseEnvironment("macos").Kind);
    }

    [Fact]
    public void Parser_AcceptsWorkspaceAndSourceCommands()
    {
        var init = GuideCommandParser.Parse([
            "init", "--workspace", ".", "--profile", "application",
            "--capability", "microservice", "--capability", "ui"
        ]);
        Assert.Equal("init", init.Name);
        Assert.True(Path.IsPathRooted(init.WorkspacePath));
        Assert.Equal("application", init.Profile);
        Assert.Equal(["microservice", "ui"], init.Capabilities);

        var bind = GuideCommandParser.Parse([
            "source", "bind", "--repository", "monica",
            "--source-path", ".", "--source-ref", "v1.0.0",
            "--apply", "--plan-digest", new string('d', 64)
        ]);
        Assert.Equal("source", bind.Name);
        Assert.Equal("bind", bind.SourceAction);
        Assert.Equal("monica", bind.Repository);
        Assert.True(bind.Apply);

        var list = GuideCommandParser.Parse(["source", "list"]);
        Assert.Equal("list", list.SourceAction);
        Assert.False(list.Apply);
    }

    [Fact]
    public void Parser_RejectsInvalidWorkspaceAndSourceUsage()
    {
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["init"]));
        Assert.Throws<GuideUsageException>(() =>
            GuideCommandParser.Parse(["source", "resolve", "--repository", "monica", "--source-ref", "v1.0.0"]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["init", "--workspace", ".", "--target", "shared"]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["configure", "--capability", "ui"]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["source"]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["source", "push"]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["source", "resolve"]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["source", "bind", "--repository", "monica"]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["source", "list", "--repository", "monica", "--source-path", "."]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["source", "list", "--apply", "--plan-digest", new string('e', 64)]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["overview", "--workspace", "."]));
        Assert.Throws<GuideUsageException>(() => GuideCommandParser.Parse(["forget", "--workspace", ".", "--repository", "monica"]));
    }

    [Fact]
    public async Task Runner_UsesStableJsonEnvelopeAndExitCodes()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await GuideCommandRunner.RunAsync(
            Product,
            ["overview", "--locale", "zh-CN", "--json"],
            output,
            error,
            TestContext.Current.CancellationToken,
            currentVersion: static () => "0.3.0");
        using var document = JsonDocument.Parse(output.ToString());

        Assert.Equal(1, exitCode);
        Assert.Equal(GuideContractVersions.CURRENT, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("0.3.0", document.RootElement.GetProperty("productVersion").GetString());
        Assert.Equal("warning", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("checks").ValueKind);
        Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("summary").ValueKind);
        Assert.False(document.RootElement.TryGetProperty("plan", out _));
        Assert.Equal(string.Empty, error.ToString());

        output.GetStringBuilder().Clear();
        var usageExit = await GuideCommandRunner.RunAsync(
            Product,
            ["unknown"],
            output,
            error,
            TestContext.Current.CancellationToken);
        Assert.Equal(2, usageExit);
    }

    [Fact]
    public async Task Runner_RefusesMachinePolicyCommandsForProductsThatDoNotOwnThem()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        string[][] machinePolicyCommands = [["source", "list"], ["issue", "status"]];
        foreach (var arguments in machinePolicyCommands)
        {
            var exitCode = await GuideCommandRunner.RunAsync(
                Product,
                arguments,
                output,
                error,
                TestContext.Current.CancellationToken);

            Assert.Equal(2, exitCode);
            Assert.Contains(
                "does not own the machine-global agent policy",
                error.ToString(),
                StringComparison.Ordinal);
        }

        // The refusal usage of a non-owner omits the policy commands it refuses to run.
        Assert.DoesNotContain("guide source", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("guide issue", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public void Paths_HonorNarrowOverridesAndWslConversionIsRoundTrip()
    {
        var environment = new FakeHostEnvironment(
            Path.GetTempPath(),
            engineRoot: Path.Combine(Path.GetTempPath(), "guide-test-engine-root"),
            productRoot: Path.Combine(Path.GetTempPath(), "guide-test-product-root"));
        var enginePaths = GuidePaths.ForCurrentUser(environment);
        var productPaths = AgentProductPaths.ForCurrentUser(KnownAgentProducts.MonicaWorkflow, environment);

        Assert.Equal(Path.GetFullPath(environment.EngineRoot), enginePaths.EngineDataRoot);
        Assert.EndsWith(
            Path.Combine("state", "guide.json"),
            enginePaths.GuideLedgerFile,
            StringComparison.Ordinal);
        Assert.Equal(Path.GetFullPath(environment.ProductRoot), productPaths.ProductDataRoot);
        Assert.EndsWith(
            Path.Combine("configuration", "server.json"),
            productPaths.ServerConfigurationFile,
            StringComparison.Ordinal);
        Assert.True(GuideHostPath.TryConvertWslPathToWindows("/mnt/d/Code/Monica", out var windows));
        Assert.Equal("D:\\Code\\Monica", windows);
        Assert.True(GuideHostPath.TryConvertWindowsPathToWsl(windows, out var wsl));
        Assert.Equal("/mnt/d/Code/Monica", wsl);
    }

    [Fact]
    public async Task Runner_ReportsCorruptLedgerAsAStableUnhealthyJsonEnvelope()
    {
        var root = Path.Combine(Path.GetTempPath(), $"guide-corrupt-{Guid.NewGuid():N}");
        var previousRoot = Environment.GetEnvironmentVariable("MONICA_GUIDE_DATA_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("MONICA_GUIDE_DATA_ROOT", root);
            var paths = new GuidePaths(root);
            Directory.CreateDirectory(Path.GetDirectoryName(paths.GuideLedgerFile)!);
            await File.WriteAllTextAsync(
                paths.GuideLedgerFile,
                "{ not valid json",
                TestContext.Current.CancellationToken);
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = await GuideCommandRunner.RunAsync(
                Product,
                ["status", "--json"],
                output,
                error,
                TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(output.ToString());

            Assert.Equal(3, exitCode);
            Assert.Equal("error", document.RootElement.GetProperty("status").GetString());
            Assert.Contains(
                document.RootElement.GetProperty("checks").EnumerateArray(),
                check => check.GetProperty("id").GetString() == "configuration.state"
                         && check.GetProperty("status").GetString() == "error");
            // Live phase lines stream to the error writer in JSON mode; the JSON envelope
            // itself must never leak off stdout.
            Assert.DoesNotContain('{', error.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MONICA_GUIDE_DATA_ROOT", previousRoot);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeHostEnvironment(string home, string engineRoot, string productRoot) : IGuideHostEnvironment
    {
        public string EngineRoot { get; } = engineRoot;
        public string ProductRoot { get; } = productRoot;
        public GuideHostPlatform Platform => GuideHostPlatform.Windows;
        public string UserHomeDirectory => home;
        public string LocalApplicationDataDirectory => home;
        public string? GetEnvironmentVariable(string name)
            => name switch
            {
                "MONICA_GUIDE_DATA_ROOT" => EngineRoot,
                "MONICA_GUIDE_PRODUCT_DATA_ROOT_MONICA_WORKFLOW" => ProductRoot,
                _ => null
            };
    }
}
