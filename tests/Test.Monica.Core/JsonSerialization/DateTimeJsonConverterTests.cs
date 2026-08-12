using System.Text.Json;
using AwesomeAssertions;
using Monica.Core.JsonSerialization.Converters;
using Monica.Core.JsonSerialization.Models;
using Xunit;

namespace Test.Monica.Core.JsonSerialization;

public sealed class DateTimeJsonConverterTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions(DateTimeWireFormat.Iso8601WallClock);

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

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void RoundTrip_WhenSpaceSeparatedFormatIsSelected_ShouldPreserveWallClockTicks(
        DateTimeKind kind)
    {
        var options = CreateSerializerOptions(DateTimeWireFormat.SpaceSeparatedWallClock);
        var value = new DateTime(2026, 7, 28, 14, 30, 0, kind).AddTicks(1_234_567);

        var json = JsonSerializer.Serialize(value, options);
        var result = JsonSerializer.Deserialize<DateTime>(json, options);

        json.Should().Be("\"2026-07-28 14:30:00.1234567\"");
        result.Ticks.Should().Be(value.Ticks);
        result.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Theory]
    [InlineData("2026-07-28T14:30:00.1234567")]
    [InlineData("2026-07-28 14:30:00.1234567")]
    public void Deserialize_WhenEitherSupportedSeparatorIsUsed_ShouldPreserveWallClockTicks(string input)
    {
        var expected = new DateTime(2026, 7, 28, 14, 30, 0).AddTicks(1_234_567);

        var result = JsonSerializer.Deserialize<DateTime>($"\"{input}\"", SerializerOptions);

        result.Ticks.Should().Be(expected.Ticks);
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

    private static JsonSerializerOptions CreateSerializerOptions(DateTimeWireFormat dateTimeFormat)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new NullableDateTimeJsonConverter(dateTimeFormat));
        options.Converters.Add(new DateTimeJsonConverter(dateTimeFormat));
        return options;
    }
}
