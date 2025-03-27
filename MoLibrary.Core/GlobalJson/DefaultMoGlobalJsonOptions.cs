using MoLibrary.Core.GlobalJson.Interfaces;
using MoLibrary.Tool.Extensions;
using System.Text.Json;

namespace MoLibrary.Core.GlobalJson;

public class DefaultMoGlobalJsonOptions : IGlobalJsonOption
{
    /// <summary>
    /// 全局的Json设置。用于Mvc等
    /// </summary>
    public static JsonSerializerOptions GlobalJsonSerializerOptions { get; set; } = new();
    ///// <summary>
    ///// 全局的后端Json设置。用于领域事件推送等。
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

    //.NET 6后 TimeZoneInfo的ID 支持跨平台自动转换
    public static TimeZoneInfo CurTimeZoneInfo => TimeExtensions.LocalTimeZoneInfo;

    /// <summary>
    /// 统一标准化从外部传入的时间
    /// 巨坑：2024-08-08T03:27:05+08:00格式的 MVC序列化会自动转化为Kind为UTC的DateTime。
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
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
    /// 统一标准化输出给外部的时间
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static DateTime NormalizeOutTime(DateTime dateTime)
    {
        return dateTime;
        //try
        //{
        //    return TimeZoneInfo.ConvertTimeFromUtc(dateTime, CurTimeZoneInfo);
        //}
        //catch (Exception e)
        //{
        //    throw new Exception("代码不要使用DateTime.Now等会使得DateTime Kind变为Local的方法，会使得后端混乱，后端统一使用UTC", e);
        //}
    }

    public JsonSerializerOptions GlobalOptions => GlobalJsonSerializerOptions;
}