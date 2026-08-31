using AwesomeAssertions;
using Monica.Guide;
using Xunit;

namespace Test.Monica.Guide;

public sealed class GuideSetupPresenterTests
{
    [Fact]
    public void PresentPlan_WhenPlanIsNull_ShouldReportAnEmptyView()
    {
        var view = GuideSetupPresenter.PresentPlan(null);

        view.IsNoOp.Should().BeFalse();
        view.ChangeCount.Should().Be(0);
        view.ChangeGroups.Should().BeEmpty();
    }

    [Fact]
    public void PresentPlan_ShouldGroupDirectoryLevelActionsByKind()
    {
        var plan = new GuidePlan(
            1,
            "configure",
            "digest",
            Applied: false,
            IsNoOp: false,
            Actions:
            [
                Action("replace-a", GuidePlanActionKind.ReplaceSkillDirectory, "skills-a"),
                Action("replace-b", GuidePlanActionKind.ReplaceSkillDirectory, "skills-b"),
                Action("remove-c", GuidePlanActionKind.DeleteSkillDirectory, "stale-skill"),
                Action("write-state", GuidePlanActionKind.WriteFile, "guide.json")
            ],
            Checks: []);

        var view = GuideSetupPresenter.PresentPlan(plan);

        view.ChangeCount.Should().Be(4);
        view.ChangeGroups.Select(static group => group.Kind).Should().Equal(
            GuidePlanActionKind.ReplaceSkillDirectory,
            GuidePlanActionKind.DeleteSkillDirectory,
            GuidePlanActionKind.WriteFile);
        view.ChangeGroups.Single(static group => group.Kind == GuidePlanActionKind.ReplaceSkillDirectory)
            .Entries.Should().HaveCount(2)
            .And.Contain(static entry => entry.Target == "skills-a");
    }

    [Fact]
    public void PresentChecks_ShouldOrderErrorsBeforeWarningsBeforeOk()
    {
        var view = GuideSetupPresenter.PresentChecks(
        [
            new GuideCheck("a.ok", GuideCheckStatus.Ok, "fine"),
            new GuideCheck("b.error", GuideCheckStatus.Error, "broken", "fix it"),
            new GuideCheck("c.warning", GuideCheckStatus.Warning, "hmm")
        ], GuideStatus.Warning);

        view.Status.Should().Be(GuideStatus.Warning);
        view.HasErrors.Should().BeFalse();
        view.OrderedItems.Select(static item => item.Id).Should().Equal("b.error", "c.warning", "a.ok");
        view.OrderedItems[0].Remediation.Should().Be("fix it");
    }

    [Fact]
    public void PresentBundle_WhenPathIsMissingOrEmpty_ShouldReportWhy()
    {
        GuideSetupPresenter.PresentBundle(null).Exists.Should().BeFalse();
        GuideSetupPresenter.PresentBundle("  ").Exists.Should().BeFalse();

        using var empty = new TempDirectory();
        var view = GuideSetupPresenter.PresentBundle(empty.Path);

        view.Exists.Should().BeFalse();
        view.Error.Should().Contain("release manifest");
    }

    [Fact]
    public void PresentBundle_WithoutAManifestIsNotInstallableEvenWithAProgramDirectory()
    {
        // A manifest-less tree is a development layout, not an immutable release; the
        // unified installer only ever installs byte-verified bundles.
        using var bundle = new TempDirectory();
        var app = Path.Combine(bundle.Path, "app");
        Directory.CreateDirectory(app);
        File.WriteAllText(Path.Combine(app, "Monica.Workflow.exe"), "marker");

        var view = GuideSetupPresenter.PresentBundle(bundle.Path);

        view.Exists.Should().BeFalse();
        view.HasManifest.Should().BeFalse();
        view.ProductVersion.Should().BeNull();
        view.Error.Should().Contain("release manifest");
    }

    [Fact]
    public void DeriveAgentPresence_ShouldCollectDetectedAgentsWithVersions()
    {
        var report = new GuideReport(
            1,
            "0.1.1",
            GuideStatus.Warning,
            [
                Detected("agent.claude.windows.detected", GuideAgent.Claude, "2.1.221 (Claude Code)"),
                new GuideCheck("agent.claude.windows.mcp", GuideCheckStatus.Ok, "configured"),
                Detected("agent.codex.windows.detected", GuideAgent.Codex, "codex-cli 0.147.0"),
                new GuideCheck("skills.windows.shared.catalog", GuideCheckStatus.Ok, "fine"),
                new GuideCheck("release.identity", GuideCheckStatus.Ok, "fine"),
                Detected("agent.codex.wsl:Ubuntu.detected", GuideAgent.Codex, null)
            ],
            new GuideSummary("status", "inspected", DateTimeOffset.UtcNow, []),
            Plan: null);

        var presence = GuideSetupPresenter.DeriveAgentPresence(report);

        presence.Should().BeEquivalentTo(
        [
            new SetupAgentPresence(GuideAgent.Codex, "windows", "codex-cli 0.147.0"),
            new SetupAgentPresence(GuideAgent.Claude, "windows", "2.1.221 (Claude Code)"),
            new SetupAgentPresence(GuideAgent.Codex, "wsl:Ubuntu", null)
        ]);
    }

    [Fact]
    public void DetectBundleRootByStructure_ShouldResolveTheBundleContainingTheSetupExecutable()
    {
        using var root = new TempDirectory();
        var bundle = System.IO.Path.Combine(root.Path, "monica-workflow-v0.1.6-win-x64");
        CreateBundle(bundle);
        File.WriteAllText(System.IO.Path.Combine(bundle, "release-manifest.json"), "{}");
        Directory.CreateDirectory(System.IO.Path.Combine(bundle, "skills"));
        File.WriteAllText(System.IO.Path.Combine(bundle, "skills", "catalog.json"), "{}");

        GuideSetupPresenter.DetectBundleRootByStructure(System.IO.Path.Combine(bundle, "setup"))
            .Should().Be(bundle);
        GuideSetupPresenter.DetectBundleRootByStructure(bundle).Should().BeNull();
        GuideSetupPresenter.DetectBundleRootByStructure(System.IO.Path.Combine(root.Path, "elsewhere"))
            .Should().BeNull();
    }

    private static GuideCheck Detected(
        string id,
        GuideAgent agent,
        string? version)
        => new(
            id,
            GuideCheckStatus.Ok,
            $"{agent} detected.",
            Details: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["environment"] = id.Split('.')[2],
                ["version"] = version ?? string.Empty
            });

    [Fact]
    public void PresentSiblingBundles_ListsOnlyManifestValidatedBundlesBesideTheGivenRoot()
    {
        using var root = new TempDirectory();
        // Marker-only trees carry no release manifest, so they are not validated rollback
        // candidates; only the fully valid bundle is listed.
        CreateBundle(System.IO.Path.Combine(root.Path, "monica-workflow-v0.1.0-win-x64"));
        CreateBundle(System.IO.Path.Combine(root.Path, "monica-workflow-v0.1.2-win-x64"), withManifest: true);
        Directory.CreateDirectory(System.IO.Path.Combine(root.Path, "monica-workflow-v0.0.9-win-x64"));

        var siblings = GuideSetupPresenter.PresentSiblingBundles(
            System.IO.Path.Combine(root.Path, "monica-workflow-v0.1.2-win-x64"),
            "monica-workflow-");

        siblings.Should().BeEmpty();
    }

    [Fact]
    public void PresentSiblingBundles_ReturnsNothingForAnUnreadableRoot()
    {
        using var root = new TempDirectory();

        GuideSetupPresenter.PresentSiblingBundles(System.IO.Path.Combine(root.Path, "missing"), "monica-workflow-")
            .Should().BeEmpty();
    }

    private static void CreateBundle(string directory, bool withManifest = false)
    {
        var app = System.IO.Path.Combine(directory, "app");
        Directory.CreateDirectory(app);
        File.WriteAllText(System.IO.Path.Combine(app, "Monica.Workflow.exe"), "marker");
        if (withManifest)
        {
            // A deliberately invalid manifest still excludes the directory: rollback
            // candidates must be verifiable end to end.
            File.WriteAllText(System.IO.Path.Combine(directory, "release-manifest.json"), "{ not-json");
        }
    }

    private static GuidePlanAction Action(
        string id,
        GuidePlanActionKind kind,
        string target)
        => new(
            id,
            kind,
            target,
            BeforeDigest: null,
            AfterDigest: null,
            Description: $"description of {id}");

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"setup-presenter-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
