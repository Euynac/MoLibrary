using Monica.Guide;
using Xunit;

namespace Test.Monica.Guide;

public sealed class GuideReleaseMetadataVersionTests
{
    [Fact]
    public void ResolveManifestPath_FindsBundleManifestFromAppDirectoryWithTrailingSeparator()
    {
        var bundle = Path.Combine(Path.GetTempPath(), $"workflow-release-layout-{Guid.NewGuid():N}");
        var app = Path.Combine(bundle, "app");
        var manifest = Path.Combine(bundle, "release-manifest.json");
        try
        {
            Directory.CreateDirectory(app);
            File.WriteAllText(manifest, "{}");

            var resolved = GuideReleaseMetadata.ResolveManifestPath(
                app + Path.DirectorySeparatorChar);

            Assert.Equal(manifest, resolved);
        }
        finally
        {
            if (Directory.Exists(bundle)) Directory.Delete(bundle, recursive: true);
        }
    }

    [Fact]
    public void ResolveManifestPath_FindsBundleManifestFromBundledSetupDirectory()
    {
        var bundle = Path.Combine(Path.GetTempPath(), $"workflow-release-layout-{Guid.NewGuid():N}");
        var setup = Path.Combine(bundle, "setup");
        var manifest = Path.Combine(bundle, "release-manifest.json");
        try
        {
            Directory.CreateDirectory(setup);
            File.WriteAllText(manifest, "{}");

            var resolved = GuideReleaseMetadata.ResolveManifestPath(setup);

            Assert.Equal(manifest, resolved);
        }
        finally
        {
            if (Directory.Exists(bundle)) Directory.Delete(bundle, recursive: true);
        }
    }

    [Fact]
    public void ValidateVersionProjection_AcceptsSemVerAndMatchingClrAssemblyVersion()
    {
        var manifest = Manifest("1.2.3.4");

        GuideReleaseMetadata.ValidateVersionProjection(manifest, expectedProductVersion: "0.3.0", expectedAssemblyVersion: "1.2.3.4");

        Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", manifest.AssemblyVersion);
    }

    [Fact]
    public void ValidateVersionProjection_RejectsAssemblyVersionDriftAgainstTheExpectation()
    {
        var manifest = Manifest("0.3.0");

        var error = Assert.Throws<InvalidDataException>(
            () => GuideReleaseMetadata.ValidateVersionProjection(manifest, expectedProductVersion: "0.3.0", expectedAssemblyVersion: "1.2.3.4"));

        Assert.Contains("AssemblyName.Version", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateVersionProjection_RejectsProductVersionAndMcpVersionDrift()
    {
        var versionDrift = Assert.Throws<InvalidDataException>(
            () => GuideReleaseMetadata.ValidateVersionProjection(Manifest("1.2.3.4"), expectedProductVersion: "9.9.9"));
        Assert.Contains("does not match the expected product", versionDrift.Message, StringComparison.Ordinal);

        var manifest = Manifest("1.2.3.4") with { McpVersion = "999.0.0" };
        var mcpDrift = Assert.Throws<InvalidDataException>(
            () => GuideReleaseMetadata.ValidateVersionProjection(manifest, expectedProductVersion: "0.3.0"));
        Assert.Contains("MCP version", mcpDrift.Message, StringComparison.Ordinal);
    }

    private static ReleaseManifest Manifest(string assemblyVersion) => new()
    {
        ProductVersion = "0.3.0",
        AssemblyVersion = assemblyVersion,
        McpVersion = "0.3.0"
    };
}
