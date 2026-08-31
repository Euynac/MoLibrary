using AwesomeAssertions;
using Monica.Guide.Setup;
using Monica.Guide;
using Xunit;

namespace Test.Monica.Guide;

public sealed class GuideUpdateFeedTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public void MatchChecksum_FindsTheDigestForTheNamedAsset()
    {
        var checksums = string.Join(
            "\n",
            "1111111111111111111111111111111111111111111111111111111111111111  monica-workflow-agent-bundle-v0.1.1.zip",
            "abcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcd1234  monica-workflow-v0.1.1-win-x64.zip",
            "2222222222222222222222222222222222222222222222222222222222222222  SHA256SUMS");

        GuideUpdateFeed.MatchChecksum(checksums, "monica-workflow-v0.1.1-win-x64.zip")
            .Should().Be("abcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcd1234");
    }

    [Fact]
    public void MatchChecksum_AcceptsCrlfAndRejectsMissingOrUppercaseEntries()
    {
        const string digest = "abcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcdabcd1234";

        GuideUpdateFeed.MatchChecksum($"{digest}  asset.zip\r\n", "asset.zip").Should().Be(digest);
        GuideUpdateFeed.MatchChecksum($"{digest}  asset.zip", "other.zip").Should().BeNull();
        GuideUpdateFeed.MatchChecksum("ABCDABCDABCDABCDABCDABCDABCDABCDABCDABCDABCDABCDABCDABCDABCD1234  asset.zip", "asset.zip")
            .Should().BeNull();
    }

    [Fact]
    public void ResolveAssets_RequiresThePairedArchiveAndChecksumAssets()
    {
        var release = Release(tag: "v0.2.0", assets:
        [
            Asset("monica-workflow-v0.2.0-win-x64.zip", 1024, "https://api.example.com/assets/zip"),
            Asset("monica-workflow-agent-bundle-v0.2.0.zip", 512, "https://api.example.com/assets/agent"),
            Asset("SHA256SUMS", 200, "https://api.example.com/assets/sums")
        ]);

        using var feed = new GuideUpdateFeed("Tairitsua/Monica.Workflow", "monica-workflow-v{version}-win-x64.zip");
        var info = feed.ResolveAssets(release);

        info.Should().NotBeNull();
        info!.Version.Should().Be("0.2.0");
        info.Tag.Should().Be("v0.2.0");
        info.ArchiveName.Should().Be("monica-workflow-v0.2.0-win-x64.zip");
        info.ArchiveSizeBytes.Should().Be(1024);
        info.ArchiveUrl.Should().Be(new Uri("https://api.example.com/assets/zip"));
        info.ChecksumsUrl.Should().Be(new Uri("https://api.example.com/assets/sums"));
    }

    [Fact]
    public void ResolveAssets_ReturnsNullWithoutChecksumsOrWithAnInvalidTag()
    {
        using var feed = new GuideUpdateFeed("Tairitsua/Monica.Workflow", "monica-workflow-v{version}-win-x64.zip");

        feed.ResolveAssets(Release(tag: "v0.2.0", assets:
        [
            Asset("monica-workflow-v0.2.0-win-x64.zip", 1, "https://api.example.com/assets/zip")
        ])).Should().BeNull();

        feed.ResolveAssets(Release(tag: "not-a-version", assets:
        [
            Asset("monica-workflow-v0.2.0-win-x64.zip", 1, "https://api.example.com/assets/zip"),
            Asset("SHA256SUMS", 1, "https://api.example.com/assets/sums")
        ])).Should().BeNull();
    }

    [Fact]
    public void ResolveAssets_SubstitutesTheRunningPlatformRidIntoTheArchiveTemplate()
    {
        var currentRid = AgentProductPlatform.CurrentRuntimeIdentifier;
        var archive = $"monica-workflow-v0.2.0-{currentRid}.zip";
        var release = Release(tag: "v0.2.0", assets:
        [
            Asset(archive, 1024, "https://api.example.com/assets/zip"),
            Asset("SHA256SUMS", 200, "https://api.example.com/assets/sums")
        ]);

        using var feed = new GuideUpdateFeed("Tairitsua/Monica.Workflow", "monica-workflow-v{version}-{rid}.zip");
        var info = feed.ResolveAssets(release);

        info.Should().NotBeNull();
        info!.ArchiveName.Should().Be(archive);
    }

    [Fact]
    public void ResolveAssets_IgnoresArchiveAssetsBuiltForAnotherPlatform()
    {
        // The template names the running platform, so an asset list carrying only a
        // different platform's archive must not resolve even with checksums present.
        var otherRid = AgentProductPlatform.CurrentRuntimeIdentifier == "win-x64" ? "linux-x64" : "win-x64";
        var release = Release(tag: "v0.2.0", assets:
        [
            Asset($"monica-workflow-v0.2.0-{otherRid}.zip", 1, "https://api.example.com/assets/zip"),
            Asset("SHA256SUMS", 1, "https://api.example.com/assets/sums")
        ]);

        using var feed = new GuideUpdateFeed("Tairitsua/Monica.Workflow", "monica-workflow-v{version}-{rid}.zip");
        feed.ResolveAssets(release).Should().BeNull();
    }

    [Theory]
    [InlineData("v0.2.0", "0.1.1", 1)]
    [InlineData("0.1.1", "v0.2.0", -1)]
    [InlineData("0.1.1", "0.1.1", 0)]
    [InlineData("0.2.0", "0.2.0-rc.1", 1)]
    [InlineData("0.2.0-preview.2", "0.2.0-preview.10", -1)]
    [InlineData("v1.0.0", "0.9.9", 1)]
    public void CompareVersions_OrdersSemVerTagsIncludingPrereleases(string left, string right, int expectedSign)
    {
        var sign = Math.Sign(GuideUpdateFeed.CompareVersions(left, right));
        sign.Should().Be(expectedSign);
    }

    [Fact]
    public async Task CheckLatestAsync_ReportsAnUpdateOnlyWhenTheFeedVersionIsNewer()
    {
        var handler = new FakeHandler(_ => Json(Release(tag: "v0.2.0", assets:
        [
            Asset("monica-workflow-v0.2.0-win-x64.zip", 1, "https://api.example.com/assets/zip"),
            Asset("SHA256SUMS", 1, "https://api.example.com/assets/sums")
        ])));

        using var feed = new GuideUpdateFeed("Tairitsua/Monica.Workflow", "monica-workflow-v{version}-win-x64.zip", new HttpClient(handler));
        var check = await feed.CheckLatestAsync("0.1.1", CancellationToken);

        check.Error.Should().BeNull();
        check.UpdateAvailable.Should().BeTrue();
        check.Latest!.Version.Should().Be("0.2.0");

        var sameVersion = await feed.CheckLatestAsync("0.2.0", CancellationToken);
        sameVersion.UpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckLatestAsync_SurfacesFeedErrorsWithoutThrowing()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));

        using var feed = new GuideUpdateFeed("Tairitsua/Monica.Workflow", "monica-workflow-v{version}-win-x64.zip", new HttpClient(handler));
        var check = await feed.CheckLatestAsync("0.1.1", CancellationToken);

        check.UpdateAvailable.Should().BeFalse();
        check.Latest.Should().BeNull();
        check.Error.Should().Contain("404");
    }

    private static HttpResponseMessage Json(object payload) => new(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json")
    };

    private static GuideUpdateFeed.GitHubRelease Release(string tag, IReadOnlyList<GuideUpdateFeed.GitHubAsset> assets)
        => new(tag, $"Monica Workflow {tag}", "notes", DateTimeOffset.UtcNow, Draft: false, Prerelease: false, assets);

    private static GuideUpdateFeed.GitHubAsset Asset(string name, long size, string url)
        => new(name, size, url);

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
