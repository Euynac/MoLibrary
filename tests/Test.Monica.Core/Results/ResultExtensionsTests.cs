using System.Collections.Generic;
using System.Net;
using AwesomeAssertions;
using Monica.Core.Results;
using Monica.Core.Results.Abstractions;
using Xunit;

namespace Test.Monica.Core.Results;

public class ResultExtensionsTests
{
    [Fact]
    public void ToHttpStatusCode_WhenResultIsAConflict_ShouldReturnHttp409()
    {
        var result = Res.Fail("stale preview", ResStatus.Conflict);

        result.ToHttpStatusCode().Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public void WithDetail_WhenCalledOnFailure_ShouldPreserveMessageAndStatus()
    {
        var result = Res.Fail("x").WithDetail("ids");

        result.Message.Should().Be("x");
        result.Status.Should().Be(ResStatus.BadRequest);
        GetMetadata(result).Should().Contain("detail", "ids");
    }

    [Fact]
    public void WithDetail_WhenCalledOnGenericResult_ShouldPreserveData()
    {
        var detail = new { MissingIds = new[] { 1L, 2L } };
        var result = Res.Ok(42).WithDetail(detail);

        result.Data.Should().Be(42);
        result.Status.Should().Be(ResStatus.Ok);
        GetMetadata(result).Should().Contain("detail", detail);
    }

    [Fact]
    public void WithDetail_WhenCalledWithFormat_ShouldStoreFormattedDetail()
    {
        var result = Res.Fail("x").WithDetail("missing: {0}", 42);

        GetMetadata(result).Should().Contain("detail", "missing: 42");
    }

    [Fact]
    public void WithDetail_WhenDetailIsNull_ShouldNotCreateMetadata()
    {
        var result = Res.Fail("x").WithDetail(null);

        result.Metadata.Should().BeNull();
    }

    [Fact]
    public void WithDetail_WhenConvertedWithData_ShouldCarryMetadata()
    {
        var detail = new { MissingIds = new[] { 1L, 2L } };
        var result = Res.Fail("x").WithDetail(detail).WithData("payload");

        result.Data.Should().Be("payload");
        result.Message.Should().Be("x");
        result.Status.Should().Be(ResStatus.BadRequest);
        GetMetadata(result).Should().Contain("detail", detail);
    }

    private static IDictionary<string, object?> GetMetadata(IResultEnvelope result)
    {
        result.Metadata.Should().NotBeNull();
        return result.Metadata!;
    }
}
