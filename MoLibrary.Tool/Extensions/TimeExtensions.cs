using System;

namespace MoLibrary.Tool.Extensions;
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

    public static readonly TimeZoneInfo LocalTimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

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
    #region 格式化

    /// <summary>
    /// Time interval conversion to Chinese format <paramref name="duration"/>.Days 天 <paramref name="duration"/>.Hours 小时 <paramref name="duration"/>.Minutes 分 <paramref name="duration"/>.Seconds 秒
    /// </summary>
    /// <param name="duration"></param>
    /// <returns></returns>
    public static string ToZhFormatString(this TimeSpan duration)
    {
        var days = duration.Days;
        var hours = duration.Hours;
        var minutes = duration.Minutes;
        var seconds = duration.Seconds;
        var milliseconds = duration.Milliseconds;
        return days.BeIfNotDefault($"{days}天")
               + hours.BeIfNotDefault($"{hours}小时")
               + minutes.BeIfNotDefault($"{minutes}分钟")
               + (milliseconds.BeIfNotDefault($"{seconds + milliseconds / 1000.0}秒") ?? seconds.BeIfNotDefault($"{seconds}秒"));
    }

    #endregion

    /// <summary>
    /// 时间戳（格林威治时间1970年01月01日00时00分00秒）类型
    /// </summary>
    public enum TimeStampType
    {
        /// <summary>
        /// 总秒数（10位）
        /// </summary>
        Unix,
        /// <summary>
        /// 总毫秒数（13位）
        /// </summary>
        Javascript
    }
    #region 时间类拓展
    /// <summary>
    /// Get the time span of given date time to that next minute.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static TimeSpan NextMinuteSpan(this DateTime dateTime) =>
        dateTime - NextMinute(dateTime);
    /// <summary>
    /// Get the time span of given date time to that next hour.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static TimeSpan NextHourSpan(this DateTime dateTime) =>
        dateTime - NextMinute(dateTime);
    /// <summary>
    /// Get the time span of given date time to that next day 00:00.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static TimeSpan NextDaySpan(this DateTime dateTime) =>
        dateTime - NextDay(dateTime);
    /// <summary>
    /// Get the date time of given date time to that next minute.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime NextMinute(this DateTime dateTime)
    {
        var timeBase = dateTime.AddMinutes(1);
        return new DateTime(timeBase.Year, timeBase.Month, timeBase.Day, timeBase.Hour, timeBase.Minute, 0);
    }
    /// <summary>
    /// Get the date time of given date time to that next hour.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime NextHour(this DateTime dateTime)
    {
        var timeBase = dateTime.AddHours(1);
        return new DateTime(timeBase.Year, timeBase.Month, timeBase.Day, timeBase.Hour, 0, 0);
    }
    /// <summary>
    /// Get the date time of given date time to that next day 00:00.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime NextDay(this DateTime dateTime)
    {
        var timeBase = dateTime.AddDays(1);
        return new DateTime(timeBase.Year, timeBase.Month, timeBase.Day, 0, 0, 0);
    }

    /// <summary>
    /// 获取指定类型的时间戳的 <see cref="DateTime"/> 表示形式
    /// </summary>
    /// <param name="timestamp">时间戳</param>
    /// <param name="timeStampType">指定类型，默认Unix（秒为单位）</param>
    /// <returns>注意是以本地时区为准的</returns>
    public static DateTime ToDateTime(this long timestamp, TimeStampType timeStampType = TimeStampType.Unix)
    {
        var startTime = TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1), TimeZoneInfo.Local);
        var daTime = new DateTime();
        switch (timeStampType)
        {
            case TimeStampType.Unix:
                daTime = startTime.AddSeconds(timestamp);
                break;
            case TimeStampType.Javascript:
                daTime = startTime.AddMilliseconds(timestamp);
                break;
        }
        return daTime;
    }
    /// <summary>
    /// DateTime转时间戳
    /// </summary>
    /// <param name="dateTime"></param>
    /// <param name="timeStampType"></param>
    /// <returns>注意是以本地时区为准的</returns>
    public static long ToTimeStamp(this DateTime dateTime, TimeStampType timeStampType = TimeStampType.Unix)
    {
        var startTime = TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1), TimeZoneInfo.Local);
        long timestamp = 0;
        switch (timeStampType)
        {
            case TimeStampType.Unix:
                timestamp = (long)(dateTime - startTime).TotalSeconds;
                break;
            case TimeStampType.Javascript:
                timestamp = (long)(dateTime - startTime).TotalMilliseconds;
                break;
        }
        return timestamp;
    }
    /// <summary>
    /// 转换为中国式星期几的表述（星期天为第七天）
    /// </summary>
    /// <param name="week"></param>
    /// <returns>1-7对应星期一到星期天</returns>
    public static ChineseWeeks ToChineseWeek(this DayOfWeek week) => week == DayOfWeek.Sunday ? ChineseWeeks.Sunday : (ChineseWeeks)week;

    #endregion
    /// <summary>
    /// Chinese week.
    /// </summary>
    public enum ChineseWeeks
    {
        Monday = 1,
        Tuesday = 2,
        Wednesday = 3,
        Thursday = 4,
        Friday = 5,
        Saturday = 6,
        Sunday = 7,
    }

    /// <summary>
    /// Round given time to second. (discard millisecond)
    /// </summary>
    /// <param name="time"></param>
    /// <returns></returns>
    public static DateTime RoundToSecond(this DateTime time) =>
        new(time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second);

    /// <summary>
    /// Combine given and time from given DateTime.
    /// </summary>
    /// <param name="date"></param>
    /// <param name="time"></param>
    /// <returns></returns>
    public static DateTime CombineDateAndTime(this DateTime date, DateTime time) =>
        new(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second, time.Millisecond);

    public enum DateTimePart
    {
        Year,
        Month,
        Day,
        Hour,
        Minute,
        Second,
        Millisecond
    }
    /// <summary>
    /// Returns a <see cref="T:System.DateOnly" /> instance that is set to the date part of the specified <paramref name="dateTime" />.
    /// </summary>
    /// <param name="dateTime">The <see cref="T:System.DateTime" /> instance.</param>
    /// <returns>The <see cref="T:System.DateOnly" /> instance composed of the date part of the specified input time <paramref name="dateTime" /> instance.</returns>
    public static DateOnly ToDateOnly(this DateTime dateTime) => DateOnly.FromDateTime(dateTime);
    /// <summary>
    /// Constructs a <see cref="T:System.TimeOnly" /> object from a <see cref="T:System.DateTime" /> representing the time of the day in this <see cref="T:System.DateTime" /> object.
    /// </summary>
    /// <param name="dateTime">The <see cref="T:System.DateTime" /> object to extract the time of the day from.</param>
    /// <returns>A <see cref="T:System.TimeOnly" /> object representing time of the day specified in the <see cref="T:System.DateTime" /> object.</returns>
    public static TimeOnly ToTimeOnly(this DateTime dateTime) => TimeOnly.FromDateTime(dateTime);
    /// <summary>
    /// Converts a <see cref="T:System.DateOnly" /> object to a <see cref="T:System.DateTime" /> object using TimeOnly.Minvalue as the time.
    /// </summary>
    /// <param name="dateOnly"></param>
    /// <returns></returns>
    public static DateTime ToDateTime(this DateOnly dateOnly) => dateOnly.ToDateTime(TimeOnly.MinValue);
    /// <summary>
    /// Truncate given date time to given part.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <param name="part"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static DateTime Truncate(DateTime dateTime, DateTimePart part)
    {
        return part switch
        {
            DateTimePart.Year => new DateTime(dateTime.Year, 0, 0),
            DateTimePart.Month => new DateTime(dateTime.Year, dateTime.Month, 0),
            DateTimePart.Day => new DateTime(dateTime.Year, dateTime.Month, dateTime.Day),
            DateTimePart.Hour => new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, 0, 0),
            DateTimePart.Minute => new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour,
                dateTime.Minute, 0),
            DateTimePart.Second => new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour,
                dateTime.Minute, dateTime.Second),
            DateTimePart.Millisecond => new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour,
                dateTime.Minute, dateTime.Second, dateTime.Millisecond),
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, null)
        };
    }
}