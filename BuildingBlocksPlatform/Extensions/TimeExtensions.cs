using BuildingBlocksPlatform.Features;
using Koubot.Tool.Extensions;

namespace BuildingBlocksPlatform.Extensions;
#region DateTime Interval

public class DateTimeInterval(DateTime left, DateTime right)
{
    /// <summary>
    /// 是否处于区间内
    /// </summary>
    /// <param name="time"></param>
    /// <returns></returns>
    public bool IsWithin(DateTime time)
    {
        return time >= left && time <= right;
    }

    /// <summary>
    /// 不在区间时，输出最近边界差异值。区间内差异为0。
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public TimeSpan GetIntervalError(DateTime value)
    {
        if (IsWithin(value)) return default;
        var leftError = Math.Abs((value - left).TotalSeconds);
        var rightError = Math.Abs((value - right).TotalSeconds);

        return leftError < rightError ? value - left : value - right;
    }
}
public class BaseTimeInterval(TimeSpan thresholdLeft, TimeSpan thresholdRight, DateTime baseTime)
{
    public DateTime BaseTime { get; } = baseTime;

    public DateTimeInterval Interval { get; } = new(baseTime.Subtract(thresholdLeft), baseTime.Add(thresholdRight));

    public bool IsWithin(DateTime time)
    {
        return Interval.IsWithin(time);
    }

    /// <summary>
    /// 距离的基准时间的差异值
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public TimeSpan GetAbsoluteError(DateTime value)
    {
        return BaseTime - value;
    }

    public override string ToString()
    {
        return $"-{thresholdLeft.TotalHours:0.#}h {BaseTime} +{thresholdRight.TotalHours:0.#}";
    }
}

#endregion
public static class TimeExtensions
{

    internal static readonly TimeZoneInfo LocalTimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

    /// <summary>
    /// Combine given date and time to datetime.
    /// </summary>
    /// <param name="timeOnly"></param>
    /// <param name="dateOnly"></param>
    /// <returns></returns>
    public static DateTime? ToDateTime(this TimeOnly? timeOnly, DateOnly? dateOnly = null)
    {
        return timeOnly == null ? null : dateOnly?.ToDateTime(timeOnly.Value);
    }
    /// <summary>
    /// Combine given date and time to datetime.
    /// </summary>
    /// <param name="timeOnly"></param>
    /// <param name="dateOnly"></param>
    /// <returns></returns>
    public static DateTime? ToDateTime(this TimeOnly timeOnly, DateOnly? dateOnly = null)
    {
        return dateOnly?.ToDateTime(timeOnly);
    }
    /// <summary>
    /// Convert given local date  to UTC  datetime.
    /// </summary>
    /// <param name="localDateOnly"></param>
    /// <returns></returns>
    public static DateTime FromLocalToUtc(this DateOnly localDateOnly) => localDateOnly.ToDateTime(TimeOnly.MinValue).FromLocalToUtc();

    /// <summary>
    /// Convert given UTC date  to local datetime.
    /// </summary>
    /// <param name="utcDateOnly"></param>
    /// <returns></returns>
    public static DateTime FromUtcToLocal(this DateOnly utcDateOnly) => utcDateOnly.ToDateTime(TimeOnly.MinValue).FromUtcToLocal();

    public static DateTime FromUtcToLocal(this DateOnly utcDateOnly, TimeOnly localTimeOnly) => utcDateOnly.ToDateTime(localTimeOnly).FromUtcToLocal();

    public static TimeOnly FromLocalToUtc(this TimeOnly localTimeOnly)
    {
        var currentOffset = LocalTimeZoneInfo.BaseUtcOffset;
        var utcTimeOnly = localTimeOnly.AddHours(-currentOffset.Hours);
        return utcTimeOnly;
    }

    public static TimeOnly FromUtcToLocal(this TimeOnly utcTimeOnly)
    {
        var currentOffset = LocalTimeZoneInfo.BaseUtcOffset;
        var localTimeOnly = utcTimeOnly.AddHours(currentOffset.Hours);
        return localTimeOnly;
    }

    /// <summary>
    /// Convert given UTC time to local time. (Disregard the kind of given datetime)
    /// </summary>
    /// <param name="utcDateTime"></param>
    /// <returns></returns>
    public static DateTime FromUtcToLocal(this DateTime utcDateTime)
    {
        var currentOffset = LocalTimeZoneInfo.BaseUtcOffset;
        var localTime = utcDateTime.AddHours(currentOffset.Hours);
        return DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
    }


    /// <summary>
    /// Convert given UTC time to local time. (Disregard the kind of given datetime)
    /// </summary>
    /// <param name="utcDateTime"></param>
    /// <returns></returns>
    public static DateTime? FromUtcToLocal(this DateTime? utcDateTime) =>
        utcDateTime == null ? null : FromUtcToLocal(utcDateTime.Value);

    /// <summary>
    /// Convert utc datetime to local DateOnly. (Disregard the kind of given datetime)
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime FromUtcToLocalDateOnly(this DateTime dateTime) => dateTime.FromUtcToLocal().ToDateOnly().FromLocalToUtc();

    /// <summary>
    /// Convert given local time to UTC time. (Disregard the kind of given datetime)
    /// </summary>
    /// <param name="localDateTime"></param>
    /// <returns></returns>
    public static DateTime? FromLocalToUtc(this DateTime? localDateTime) =>
        localDateTime == null ? null : FromLocalToUtc(localDateTime.Value);

    /// <summary>
    /// Convert given local time to UTC time. (Disregard the kind of given datetime)
    /// </summary>
    /// <param name="localDateTime"></param>
    /// <returns></returns>
    public static DateTime FromLocalToUtc(this DateTime localDateTime)
    {
        var currentOffset = LocalTimeZoneInfo.BaseUtcOffset;
        var localTime = localDateTime.AddHours(-currentOffset.Hours);
        return DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
    }


    public static bool EqualBySecond(this DateTime left, DateTime right)
    {
        return left.Year == right.Year && left.Month == right.Month 
                                       && left.Day == right.Day&& left.Hour ==right.Hour 
                                       && left.Minute == right.Minute&& left.Second == right.Second;
    }

    public static bool EqualBySecond(this DateTime? left, DateTime? right)
    {
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;
        return EqualBySecond(left.Value, right.Value);
    }


    /// <summary>
    /// Replace time part of given datetime with given time only.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <param name="timeOnly"></param>
    /// <returns></returns>
    public static DateTime ReplaceTime(this DateTime dateTime, TimeOnly timeOnly) =>
    new(dateTime.Year, dateTime.Month, dateTime.Day, timeOnly.Hour, timeOnly.Minute, timeOnly.Second);
}