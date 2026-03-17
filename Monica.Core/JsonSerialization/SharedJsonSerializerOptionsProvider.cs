using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Monica.Core.JsonSerialization.Interfaces;

namespace Monica.Core.JsonSerialization;

public class SharedJsonSerializerOptionsProvider : IJsonSerializerOptionsProvider
{
    /// <summary>
    /// Gets or sets the shared JSON serializer options used by MVC and other global pipelines.
    /// </summary>
    public static JsonSerializerOptions SharedSerializerOptions { get; set; } = new();
    ///// <summary>
    ///// Shared backend JSON settings for scenarios such as domain-event publishing.
    ///// </summary>
    //internal static JsonSerializerOptions GlobalBackendJsonSerializerOptions { get; set; } = new()
    //{
    //    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    //    PropertyNameCaseInsensitive = true,
    //    Converters = { new DateTimeJsonConverter(), new NullableDateTimeJsonConverter()}
    //};
    public static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss"
    ];
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
        //switch (dateTime.Kind)
        //{
        //    case DateTimeKind.Utc:
        //        return dateTime;
        //    case DateTimeKind.Local:
        //        return dateTime.ToUniversalTime();
        //}

        //return TimeZoneInfo.ConvertTimeToUtc(dateTime, CurTimeZoneInfo);
    }

    /// <summary>
    /// Normalizes <see cref="DateTime"/> values before they are written to external output.
    /// </summary>
    /// <param name="dateTime">The outbound date-time value.</param>
    /// <returns>The normalized date-time value.</returns>
    /// <exception cref="Exception">Thrown by the commented-out fallback conversion logic when enabled.</exception>
    public static DateTime NormalizeOutTime(DateTime dateTime)
    {
        return dateTime;
        //try
        //{
        //    return TimeZoneInfo.ConvertTimeFromUtc(dateTime, CurTimeZoneInfo);
        //}
        //catch (Exception e)
        //{
        //    throw new Exception("Avoid APIs such as DateTime.Now that produce DateTimeKind.Local. The backend standard is UTC.", e);
        //}
    }

    public JsonSerializerOptions SerializerOptions => SharedSerializerOptions;

    [return: NotNullIfNotNull("str")]
    public string? UsingJsonNamePolicy(string? str)
    {
        if(str == null) return null;
        return SerializerOptions.PropertyNamingPolicy?.ConvertName(str) ?? str;
    }
}
