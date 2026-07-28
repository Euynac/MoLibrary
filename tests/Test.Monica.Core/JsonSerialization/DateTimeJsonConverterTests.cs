using System.Text.Json;
using AwesomeAssertions;
using Monica.Core.JsonSerialization.Converters;
using Xunit;

namespace Test.Monica.Core.JsonSerialization;

public sealed class DateTimeJsonConverterTests
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void RoundTrip_WhenValueHasWholeSecondPrecision_ShouldUseCanonicalWallClockFormat(
        DateTimeKind kind)
    {
        var value = DateTime.SpecifyKind(new DateTime(2026, 7, 28, 14, 30, 0), kind);

        var json = JsonSerializer.Serialize(value, SerializerOptions);
        var result = JsonSerializer.Deserialize<DateTime>(json, SerializerOptions);

        json.Should().Be("\"2026-07-28T14:30:00\"");
        result.Ticks.Should().Be(value.Ticks);
        result.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void RoundTrip_WhenValueHasFractionalTicks_ShouldPreserveEveryTickWithoutKind()
    {
        var value = new DateTime(2026, 7, 28, 14, 30, 0, DateTimeKind.Utc)
            .AddTicks(1_234_567);

        var json = JsonSerializer.Serialize(value, SerializerOptions);
        var result = JsonSerializer.Deserialize<DateTime>(json, SerializerOptions);

        json.Should().Be("\"2026-07-28T14:30:00.1234567\"");
        result.Ticks.Should().Be(value.Ticks);
        result.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void RoundTrip_WhenValueIsNullable_ShouldUseTheSameCanonicalFormat()
    {
        DateTime? value = new DateTime(2026, 7, 28, 14, 30, 0, DateTimeKind.Local)
            .AddTicks(7_650_000);

        var json = JsonSerializer.Serialize(value, SerializerOptions);
        var result = JsonSerializer.Deserialize<DateTime?>(json, SerializerOptions);

        json.Should().Be("\"2026-07-28T14:30:00.765\"");
        result.Should().NotBeNull();
        result.Value.Ticks.Should().Be(value.Value.Ticks);
        result.Value.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void RoundTrip_WhenNullableValueIsNull_ShouldPreserveNull()
    {
        DateTime? value = null;

        var json = JsonSerializer.Serialize(value, SerializerOptions);
        var result = JsonSerializer.Deserialize<DateTime?>(json, SerializerOptions);

        json.Should().Be("null");
        result.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_WhenValueIsDateTimeOffset_ShouldPreserveOffsetAndInstant()
    {
        var value = new DateTimeOffset(2026, 7, 28, 14, 30, 0, TimeSpan.FromHours(8))
            .AddTicks(1_234_567);

        var json = JsonSerializer.Serialize(value, SerializerOptions);
        var result = JsonSerializer.Deserialize<DateTimeOffset>(json, SerializerOptions);

        json.Should().Be("\"2026-07-28T14:30:00.1234567+08:00\"");
        result.Offset.Should().Be(value.Offset);
        result.UtcDateTime.Ticks.Should().Be(value.UtcDateTime.Ticks);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new NullableDateTimeJsonConverter());
        options.Converters.Add(new DateTimeJsonConverter());
        return options;
    }
}
