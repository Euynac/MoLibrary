using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Monica.Core.JsonSerialization.Abstractions;

namespace Monica.Core.JsonSerialization.Services;

/// <summary>
/// Exposes the finalized JSON serializer options owned by one Monica host.
/// </summary>
public sealed class JsonSerializerOptionsProvider(
    JsonSerializerOptions serializerOptions) : IJsonSerializerOptionsProvider
{
    /// <summary>
    /// Gets the canonical outbound date-time format.
    /// </summary>
    /// <remarks>
    /// The format preserves tick precision, trims insignificant fractional zeros, and intentionally emits
    /// no offset because Monica transports <see cref="DateTime"/> as a timezone-free wall-clock value.
    /// </remarks>
    public static readonly string OutputDateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF";

    /// <summary>
    /// Gets the accepted inbound date-time formats.
    /// </summary>
    public static readonly string[] DateTimeFormats =
    [
        OutputDateTimeFormat,
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss"
    ];

    /// <summary>
    /// Preserves an inbound <see cref="DateTime"/> without applying a time-zone conversion.
    /// </summary>
    /// <param name="dateTime">The inbound date-time value.</param>
    /// <returns>The unchanged date-time value.</returns>
    public static DateTime NormalizeInTime(DateTime dateTime) => dateTime;

    /// <summary>
    /// Preserves an outbound <see cref="DateTime"/> without applying a time-zone conversion.
    /// </summary>
    /// <param name="dateTime">The outbound date-time value.</param>
    /// <returns>The unchanged date-time value.</returns>
    public static DateTime NormalizeOutTime(DateTime dateTime) => dateTime;

    /// <inheritdoc />
    public JsonSerializerOptions SerializerOptions { get; } = serializerOptions;

    /// <inheritdoc />
    [return: NotNullIfNotNull("str")]
    public string? UsingJsonNamePolicy(string? str)
    {
        if(str == null) return null;
        return SerializerOptions.PropertyNamingPolicy?.ConvertName(str) ?? str;
    }

    /// <inheritdoc />
    [return: NotNullIfNotNull("str")]
    public string? UsingJsonDictionaryKeyPolicy(string? str)
    {
        if (str == null) return null;
        return SerializerOptions.DictionaryKeyPolicy?.ConvertName(str) ?? str;
    }
}
