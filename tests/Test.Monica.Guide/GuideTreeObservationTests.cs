using Monica.Guide;
using Xunit;

namespace Test.Monica.Guide;

public sealed class GuideTreeObservationTests
{
    [Fact]
    public void BuildWslObserveScript_FramesEveryRecordKind()
    {
        var script = GuideEnvironmentRuntime.BuildWslObserveScript(
            "/home/u",
            ["/home/u", "/home/u/.agents/skills"],
            ["/home/u/.agents/skills/monica-workflow-guide/SKILL.md"],
            ["/home/u/.agents/skills/monica-workflow-guide"]);

        Assert.Contains("printf 'H\\0%s\\0'", script, StringComparison.Ordinal);
        Assert.Contains("sha256sum", script, StringComparison.Ordinal);
        Assert.Contains("find \"$1\" -mindepth 1 -printf '%y\\0%p\\0'", script, StringComparison.Ordinal);
        // Probe items reference the shell helpers so each path appears exactly once.
        Assert.Contains("p '/home/u/.agents/skills';", script, StringComparison.Ordinal);
        Assert.Contains("f '/home/u/.agents/skills/monica-workflow-guide/SKILL.md';", script, StringComparison.Ordinal);
        Assert.Contains("r '/home/u/.agents/skills/monica-workflow-guide';", script, StringComparison.Ordinal);
        // Probe items reference the shell helpers so each path appears exactly once.
        var occurrences = script.Split("monica-workflow-guide/SKILL.md", StringSplitOptions.RemoveEmptyEntries).Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void BuildWslObserveScript_ChunksLongCatalogsBelowTheCommandLineBound()
    {
        var root = "/home/user-with-a-long-name/.agents/skills";
        var files = Enumerable.Range(0, 220)
            .Select(index => $"{root}/monica-workflow-knowledge-business-rule-{index:D3}/references/very-long-reference-file-name.md")
            .ToArray();

        // Mirror the production chunker bound: every chunk must fit its wsl.exe command line.
        const int PreambleEstimate = 700;
        var estimate = PreambleEstimate;
        var chunkCount = 0;
        foreach (var file in files)
        {
            estimate += file.Length + 10;
            if (estimate >= 6_000)
            {
                chunkCount++;
                estimate = PreambleEstimate;
            }
        }

        Assert.True(chunkCount >= 2, "A large catalog must split into several bounded chunks.");
        var oneChunk = GuideEnvironmentRuntime.BuildWslObserveScript(
            root,
            files.Take(70).ToArray(),
            files.Take(70).ToArray(),
            []);
        Assert.True(oneChunk.Length < 30_000, $"A maximal chunk must stay below the command-line bound but was {oneChunk.Length} characters.");
    }

    [Fact]
    public void ParseWslObservation_ReadsSegmentsDigestsDevicesAndListings()
    {
        var segments = new Dictionary<string, (char Kind, string Device)>(StringComparer.Ordinal);
        var digests = new Dictionary<string, string>(StringComparer.Ordinal);
        var rootEntries = new Dictionary<string, IReadOnlyList<GuideFileSystemEntry>>(StringComparer.Ordinal);
        var homeDevice = "?";
        var output = string.Join('\0',
            "H", "/",
            "S", "/home/u", "d", "/",
            "S", "/home/u/.agents", "d", "/mnt/data",
            "S", "/home/u/.agents/skills", "l", "-",
            "F", "cafebabe", "/home/u/.agents/skills/skill-a/SKILL.md",
            "M", "/home/u/.agents/skills/skill-a/obsolete.md",
            "R", "/home/u/.agents/skills/skill-a",
            "f", "/home/u/.agents/skills/skill-a/SKILL.md",
            "d", "/home/u/.agents/skills/skill-a/sub",
            "S", "/home/u/.claude", "m", "-",
            string.Empty);

        GuideEnvironmentRuntime.ParseWslObservation(output, segments, ref homeDevice, digests, rootEntries);

        Assert.Equal("/", homeDevice);
        Assert.Equal('l', segments["/home/u/.agents/skills"].Kind);
        Assert.Equal('m', segments["/home/u/.claude"].Kind);
        Assert.Equal("cafebabe", digests["/home/u/.agents/skills/skill-a/SKILL.md"]);
        Assert.False(digests.ContainsKey("/home/u/.agents/skills/skill-a/obsolete.md"));
        var listing = rootEntries["/home/u/.agents/skills/skill-a"];
        Assert.Equal(2, listing.Count);
        Assert.Equal(GuideFileSystemEntryKind.File, listing[0].Kind);
        Assert.Equal(GuideFileSystemEntryKind.Directory, listing[1].Kind);
        Assert.Equal("/home/u/.agents/skills/skill-a/sub", listing[1].Path);
    }

    [Fact]
    public void ParseEntryKind_TreatsMissingAsAbsentAndFailsClosedOnUnknownMarkers()
    {
        // A missing WSL path must read as absent (null), matching the Windows branch; treating
        // it as an existing non-directory blocked every fresh WSL install (issue #1).
        Assert.Null(GuideEnvironmentRuntime.ParseEntryKind('m'));
        Assert.Null(GuideEnvironmentRuntime.ParseEntryKind('\0'));
        Assert.Equal(GuideFileSystemEntryKind.File, GuideEnvironmentRuntime.ParseEntryKind('f'));
        Assert.Equal(GuideFileSystemEntryKind.Directory, GuideEnvironmentRuntime.ParseEntryKind('d'));
        Assert.Equal(GuideFileSystemEntryKind.Redirect, GuideEnvironmentRuntime.ParseEntryKind('l'));
        Assert.Equal(GuideFileSystemEntryKind.Other, GuideEnvironmentRuntime.ParseEntryKind('o'));
        Assert.Throws<InvalidDataException>(() => GuideEnvironmentRuntime.ParseEntryKind('x'));
    }

    [Fact]
    public async Task ManagedObservation_ReportsKindsDigestsAndEntries()
    {
        var home = Path.Combine(Path.GetTempPath(), $"workflow-tree-home-{Guid.NewGuid():N}");
        var root = Path.Combine(home, "skill-a");
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        var file = Path.Combine(root, "SKILL.md");
        try
        {
            await File.WriteAllTextAsync(file, "payload", TestContext.Current.CancellationToken);
            var runtime = new GuideEnvironmentRuntime(new WindowsHostEnvironment(home));

            var tree = await runtime.ObserveTreeAsync(
                new GuideEnvironment("windows", "windows"),
                home,
                [root, file],
                [file],
                [root],
                TestContext.Current.CancellationToken);

            Assert.False(tree.Paths[root].Redirected);
            Assert.Equal(GuideFileSystemEntryKind.Directory, tree.Paths[root].Kind);
            Assert.Equal(GuideFileSystemEntryKind.File, tree.Paths[file].Kind);
            var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("payload"u8.ToArray())).ToLowerInvariant();
            Assert.Equal(expected, tree.FileDigests[file]);
            Assert.Equal(2, tree.RootEntries[root].Count);
            Assert.Contains(tree.RootEntries[root], entry => entry.Path == file);
            // A missing path resolves to no kind and no digest instead of failing the batch.
            Assert.False(tree.Paths.ContainsKey(Path.Combine(root, "missing.md")));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    private sealed class WindowsHostEnvironment(string home) : IGuideHostEnvironment
    {
        public string? OverrideRoot => null;
        public GuideHostPlatform Platform => GuideHostPlatform.Windows;
        public string UserHomeDirectory => home;
        public string LocalApplicationDataDirectory => home;
        public string? GetEnvironmentVariable(string name) => null;
    }
}
