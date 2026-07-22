using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Monica.Core.ExceptionHandling.Services;
using Monica.Core.Results;
using Xunit;

namespace Test.Monica.Core.ExceptionHandling;

public sealed class BadHttpRequestExceptionMapperTests
{
    [Fact]
    public void TryMap_JsonBindingFailure_ReturnsDetailedBadRequest()
    {
        var mapper = new BadHttpRequestExceptionMapper();
        var exception = new BadHttpRequestException(
            "Failed to read the request body.",
            new JsonException("Required properties including 'expectedValueVersion' were missing."));

        var mapped = mapper.TryMap(null, exception, CancellationToken.None, out var response);

        mapped.Should().BeTrue();
        response.Should().NotBeNull();
        response!.Status.Should().Be(ResStatus.BadRequest);
        response.Message.Should().Contain("expectedValueVersion");
    }

    [Fact]
    public void TryMap_NonBindingException_DoesNotHandleIt()
    {
        var mapper = new BadHttpRequestExceptionMapper();

        var mapped = mapper.TryMap(null, new InvalidOperationException("failure"), CancellationToken.None, out var response);

        mapped.Should().BeFalse();
        response.Should().BeNull();
    }

    [Theory]
    [InlineData(StatusCodes.Status413PayloadTooLarge, ResStatus.PayloadTooLarge)]
    [InlineData(StatusCodes.Status415UnsupportedMediaType, ResStatus.UnsupportedMediaType)]
    public void TryMap_NonBadRequestStatus_PreservesHttpStatus(int statusCode, ResStatus expectedStatus)
    {
        var mapper = new BadHttpRequestExceptionMapper();
        var exception = new BadHttpRequestException("The request cannot be processed.", statusCode);

        var mapped = mapper.TryMap(null, exception, CancellationToken.None, out var response);

        mapped.Should().BeTrue();
        response.Should().NotBeNull();
        response!.Status.Should().Be(expectedStatus);
        response.ToHttpStatusCode().Should().Be((HttpStatusCode)statusCode);
    }
}
