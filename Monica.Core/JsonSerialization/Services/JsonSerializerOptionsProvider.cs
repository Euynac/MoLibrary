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
    /// Gets the accepted inbound date-time formats.
    /// </summary>
    public static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss"
    ];

    /// <summary>
    /// Gets the canonical outbound date-time format.
    /// </summary>
    public static readonly string OutputDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// Normalizes <see cref="DateTime"/> values coming from external input.
    /// MVC can deserialize values like <c>2024-08-08T03:27:05+08:00</c> into a UTC-kind <see cref="DateTime"/>,
    /// so all inbound normalization is centralized here.
    /// </summary>
    /// <param name="dateTime">The inbound date-time value.</param>
    /// <returns>The normalized date-time value.</returns>
    public static DateTime NormalizeInTime(DateTime dateTime)
    {
        return dateTime;
    }

    /// <summary>
    /// Normalizes <see cref="DateTime"/> values before they are written to external output.
    /// </summary>
    /// <param name="dateTime">The outbound date-time value.</param>
    /// <returns>The normalized date-time value.</returns>
    public static DateTime NormalizeOutTime(DateTime dateTime)
    {
        return dateTime;
    }

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
