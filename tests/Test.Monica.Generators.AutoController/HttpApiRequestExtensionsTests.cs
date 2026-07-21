using AwesomeAssertions;
using Monica.WebApi.RpcClient.Extensions;
using Xunit;

namespace Test.Monica.Generators.AutoController;

public sealed class HttpApiRequestExtensionsTests
{
    [Fact]
    public void BuildApiRequestUri_WhenOptionalRouteValueIsNull_ShouldRemoveSegmentAndExcludePropertyFromQuery()
    {
        var request = new OptionalRouteRequest(null, "active");

        var uri = request.BuildApiRequestUri("api/orders/{Id?}", includeQueryString: true);

        uri.Should().Be("api/orders?filter=active");
    }

    [Fact]
    public void BuildApiRequestUri_WhenNullRouteValueHasDefault_ShouldUseDefaultAndExcludePropertyFromQuery()
    {
        var request = new DefaultRouteRequest(null, "active");

        var uri = request.BuildApiRequestUri("api/orders/{Culture=en-US}", includeQueryString: true);

        uri.Should().Be("api/orders/en-US?filter=active");
    }

    [Fact]
    public void BuildApiRequestUri_WhenCatchAllHasValue_ShouldEscapeValueAndExcludePropertyFromQuery()
    {
        var request = new CatchAllRouteRequest("folder A/child", "active");

        var uri = request.BuildApiRequestUri("api/files/{**Path}", includeQueryString: true);

        uri.Should().Be("api/files/folder%20A%2Fchild?filter=active");
    }

    [Fact]
    public void BuildApiRequestUri_WhenRequiredRouteValueIsNull_ShouldFailClearly()
    {
        var request = new RequiredRouteRequest(null);

        var build = () => request.BuildApiRequestUri("api/orders/{Id}", includeQueryString: false);

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*Route property 'Id'*");
    }

    private sealed record OptionalRouteRequest(long? Id, string Filter);

    private sealed record DefaultRouteRequest(string? Culture, string Filter);

    private sealed record CatchAllRouteRequest(string Path, string Filter);

    private sealed record RequiredRouteRequest(long? Id);
}
