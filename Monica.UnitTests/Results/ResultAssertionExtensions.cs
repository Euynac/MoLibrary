using AwesomeAssertions;
using Monica.Core.Results;

namespace Monica.UnitTests.Results;

/// <summary>
/// Provides Monica-specific result assertions for infrastructure and application tests.
/// </summary>
public static class ResultAssertionExtensions
{
    /// <summary>
    /// Asserts that the result succeeded and optionally verifies the message.
    /// </summary>
    public static Res ShouldSucceed(this Res result, string? expectedMessage = null)
    {
        result.Status.Should().Be(ResStatus.Ok);
        if (expectedMessage is not null)
        {
            result.Message.Should().Be(expectedMessage);
        }

        return result;
    }

    /// <summary>
    /// Asserts that the result failed and optionally verifies the status and message.
    /// </summary>
    public static Res ShouldFail(
        this Res result,
        ResStatus expectedStatus = ResStatus.BadRequest,
        string? expectedMessage = null)
    {
        result.Status.Should().Be(expectedStatus);
        if (expectedMessage is not null)
        {
            result.Message.Should().Be(expectedMessage);
        }

        return result;
    }

    /// <summary>
    /// Asserts that the result succeeded and returns the payload for additional checks.
    /// </summary>
    public static T? ShouldSucceed<T>(this Res<T> result, string? expectedMessage = null)
    {
        result.Status.Should().Be(ResStatus.Ok);
        if (expectedMessage is not null)
        {
            result.Message.Should().Be(expectedMessage);
        }

        return result.Data;
    }

    /// <summary>
    /// Asserts that the result failed and optionally verifies the status and message.
    /// </summary>
    public static Res<T> ShouldFail<T>(
        this Res<T> result,
        ResStatus expectedStatus = ResStatus.BadRequest,
        string? expectedMessage = null)
    {
        result.Status.Should().Be(expectedStatus);
        if (expectedMessage is not null)
        {
            result.Message.Should().Be(expectedMessage);
        }

        return result;
    }

    /// <summary>
    /// Asserts that the paged result succeeded and returns the page payload.
    /// </summary>
    public static ResPaged<T>.PageData ShouldSucceed<T>(this ResPaged<T> result, string? expectedMessage = null)
    {
        result.Status.Should().Be(ResStatus.Ok);
        if (expectedMessage is not null)
        {
            result.Message.Should().Be(expectedMessage);
        }

        return result.Data;
    }
}
