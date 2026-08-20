using System.Text;
using System.Text.Json;
using Monica.Tool.Diagnostics;

namespace Test.Monica.Tool.Diagnostics;

public sealed class DebugJsonTests
{
    [Fact]
    public void ToJsonString_WhenTextContainsJsonEscapes_ShouldPreserveTheValue()
    {
        var json = new { Value = @"\n\path" }.ToJsonString();

        using var document = JsonDocument.Parse(json!);
        document.RootElement.GetProperty("Value").GetString().Should().Be(@"\n\path");
    }

    [Fact]
    public void ToJsonStringForce_WhenOutputExceedsLimit_ShouldReturnBoundedJson()
    {
        const int limit = 128;
        var options = new ForceSerializeOptions { MaxOutputSizeBytes = limit };

        var json = new { Value = new string('x', 10_000) }.ToJsonStringForce(options);

        Encoding.UTF8.GetByteCount(json!).Should().BeLessThanOrEqualTo(limit);
        using var document = JsonDocument.Parse(json!);
        document.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public void ToJsonStringForce_WhenGraphContainsSharedReference_ShouldSerializeBothPaths()
    {
        var shared = new SharedNode { Value = 7 };

        var json = new SharedRoot { First = shared, Second = shared }.ToJsonStringForce();

        using var document = JsonDocument.Parse(json!);
        document.RootElement.GetProperty(nameof(SharedRoot.First)).GetProperty(nameof(SharedNode.Value)).GetInt32()
            .Should().Be(7);
        document.RootElement.GetProperty(nameof(SharedRoot.Second)).GetProperty(nameof(SharedNode.Value)).GetInt32()
            .Should().Be(7);
    }

    [Fact]
    public void ToJsonStringForce_WhenCollectionIsNormallySerializable_ShouldStillHonorItemLimit()
    {
        var options = new ForceSerializeOptions { MaxCollectionItems = 2 };

        var json = Enumerable.Range(1, 100).ToArray().ToJsonStringForce(options);

        using var document = JsonDocument.Parse(json!);
        document.RootElement.GetArrayLength().Should().Be(3);
        document.RootElement[2].GetString().Should().Contain("TRUNCATED");
    }

    [Fact]
    public void ToJsonStringForce_WhenJsonElementIsLarge_ShouldHonorLimits()
    {
        using var source = JsonDocument.Parse("{\"Values\":[1,2,3,4],\"LongText\":\"abcdefgh\"}");
        var options = new ForceSerializeOptions { MaxCollectionItems = 2, MaxStringLength = 3 };

        var json = source.RootElement.ToJsonStringForce(options);

        using var document = JsonDocument.Parse(json!);
        document.RootElement.GetProperty("Values").GetArrayLength().Should().Be(3);
        document.RootElement.GetProperty("LongText").GetString().Should().StartWith("abc").And.Contain("TRUNCATED");
    }

    [Fact]
    public void ToJsonStringForce_WhenOutputLimitCannotContainValidJson_ShouldRejectOption()
    {
        var options = new ForceSerializeOptions { MaxOutputSizeBytes = 3 };

        var act = () => new { Value = 1 }.ToJsonStringForce(options);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ToJsonStringForce_WhenCustomOptionsAreProvided_ShouldStillForceAndBoundSerialization()
    {
        var forceOptions = new ForceSerializeOptions { MaxOutputSizeBytes = 128 };
        var serializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var json = new { LongValue = new string('x', 10_000) }
            .ToJsonStringForce(forceOptions, customOptions: serializerOptions);

        Encoding.UTF8.GetByteCount(json!).Should().BeLessThanOrEqualTo(128);
        JsonDocument.Parse(json!).Dispose();
    }

    [Fact]
    public void ToJsonStringForce_WhenExceptionDataReferencesException_ShouldReportCycle()
    {
        var exception = new InvalidOperationException("failure");
        exception.Data["self"] = exception;

        var json = exception.ToJsonStringForce();

        json.Should().Contain("CIRCULAR_REFERENCE");
        JsonDocument.Parse(json!).Dispose();
    }

    private sealed class SharedRoot
    {
        public required SharedNode First { get; init; }
        public required SharedNode Second { get; init; }
    }

    private sealed class SharedNode
    {
        public int Value { get; init; }
    }
}
