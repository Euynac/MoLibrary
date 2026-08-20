namespace Monica.Tool.Extensions;
#region DateTime Interval

public class DateTimeInterval(DateTime left, DateTime right)
{
    /// <summary>
    /// Is it within the interval?
    /// </summary>
    /// <param name="time"></param>
    /// <returns></returns>
    public bool IsWithin(DateTime time)
    {
        return time >= left && time <= right;
    }

    /// <summary>
    /// When not in the interval, output the nearest boundary difference value. The difference within the interval is 0.
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
    /// Difference value of distance from base time
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
    /// Converts a wall-clock time from the operating system's local timezone to UTC using its base offset.
    /// A date-aware conversion should be used when daylight-saving transitions matter.
    /// </summary>
    public static TimeOnly FromLocalToUtc(this TimeOnly localTimeOnly)
    {
        var currentOffset = TimeZoneInfo.Local.BaseUtcOffset;
        var utcTimeOnly = localTimeOnly.Add(-currentOffset);
        return utcTimeOnly;
    }

    /// <summary>
    /// Converts a UTC wall-clock time to the operating system's local timezone using its base offset.
    /// A date-aware conversion should be used when daylight-saving transitions matter.
    /// </summary>
    public static TimeOnly FromUtcToLocal(this TimeOnly utcTimeOnly)
    {
        var currentOffset = TimeZoneInfo.Local.BaseUtcOffset;
        var localTimeOnly = utcTimeOnly.Add(currentOffset);
        return localTimeOnly;
    }

    /// <summary>
    /// Converts a UTC value to the operating system's local timezone, including daylight-saving rules.
    /// </summary>
    /// <param name="utcDateTime"></param>
    /// <returns></returns>
    public static DateTime FromUtcToLocal(this DateTime utcDateTime)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc),
            TimeZoneInfo.Local);
    }


    /// <summary>
    /// Converts a nullable UTC value to the operating system's local timezone, including daylight-saving rules.
    /// </summary>
    /// <param name="utcDateTime"></param>
    /// <returns></returns>
    public static DateTime? FromUtcToLocal(this DateTime? utcDateTime) =>
        utcDateTime == null ? null : FromUtcToLocal(utcDateTime.Value);


    /// <summary>
    /// Converts a nullable wall-clock value in the operating system's local timezone to UTC.
    /// </summary>
    /// <param name="localDateTime"></param>
    /// <returns></returns>
    public static DateTime? FromLocalToUtc(this DateTime? localDateTime) =>
        localDateTime == null ? null : FromLocalToUtc(localDateTime.Value);

    /// <summary>
    /// Converts a wall-clock value in the operating system's local timezone to UTC.
    /// </summary>
    /// <param name="localDateTime"></param>
    /// <returns></returns>
    public static DateTime FromLocalToUtc(this DateTime localDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified),
            TimeZoneInfo.Local);
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
        dateTime.Date.Add(timeOnly.ToTimeSpan());
    #region Formatting

    /// <summary>
    /// Converts a time interval into a human-readable Chinese duration string:
    /// <paramref name="duration"/>.Days days, <paramref name="duration"/>.Hours hours,
    /// <paramref name="duration"/>.Minutes minutes, and <paramref name="duration"/>.Seconds seconds.
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
    /// Timestamp (January 1, 1970 00:00:00 GMT) type
    /// </summary>
    public enum TimeStampType
    {
        /// <summary>
        /// Total seconds (10 digits)
        /// </summary>
        Unix,
        /// <summary>
        /// Total milliseconds (13 bits)
        /// </summary>
        Javascript
    }
    #region Time Extensions
    /// <summary>
    /// Get the time span of given date time to that next minute.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static TimeSpan NextMinuteSpan(this DateTime dateTime) =>
        NextMinute(dateTime) - dateTime;
    /// <summary>
    /// Get the time span of given date time to that next hour.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static TimeSpan NextHourSpan(this DateTime dateTime) =>
        NextHour(dateTime) - dateTime;
    /// <summary>
    /// Get the time span of given date time to that next day 00:00.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static TimeSpan NextDaySpan(this DateTime dateTime) =>
        NextDay(dateTime) - dateTime;
    /// <summary>
    /// Get the date time of given date time to that next minute.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime NextMinute(this DateTime dateTime)
    {
        var timeBase = dateTime.AddMinutes(1);
        return new DateTime(timeBase.Ticks - timeBase.Ticks % TimeSpan.TicksPerMinute, timeBase.Kind);
    }
    /// <summary>
    /// Get the date time of given date time to that next hour.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime NextHour(this DateTime dateTime)
    {
        var timeBase = dateTime.AddHours(1);
        return new DateTime(timeBase.Ticks - timeBase.Ticks % TimeSpan.TicksPerHour, timeBase.Kind);
    }
    /// <summary>
    /// Get the date time of given date time to that next day 00:00.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime NextDay(this DateTime dateTime)
    {
        return dateTime.Date.AddDays(1);
    }

    /// <summary>
    /// Gets the <see cref="DateTime"/> representation of a timestamp of the specified type
    /// </summary>
    /// <param name="timestamp">Timestamp</param>
    /// <param name="timeStampType">Specify type, default Unix (in seconds)</param>
    /// <returns>Note that this is based on the local time zone</returns>
    public static DateTime ToDateTime(this long timestamp, TimeStampType timeStampType = TimeStampType.Unix)
    {
        var dateTimeOffset = timeStampType switch
        {
            TimeStampType.Unix => DateTimeOffset.FromUnixTimeSeconds(timestamp),
            TimeStampType.Javascript => DateTimeOffset.FromUnixTimeMilliseconds(timestamp),
            _ => throw new ArgumentOutOfRangeException(nameof(timeStampType), timeStampType, null)
        };

        return dateTimeOffset.LocalDateTime;
    }
    /// <summary>
    /// DateTime to timestamp
    /// </summary>
    /// <param name="dateTime"></param>
    /// <param name="timeStampType"></param>
    /// <returns>Note that this is based on the local time zone</returns>
    public static long ToTimeStamp(this DateTime dateTime, TimeStampType timeStampType = TimeStampType.Unix)
    {
        var dateTimeOffset = new DateTimeOffset(dateTime);
        return timeStampType switch
        {
            TimeStampType.Unix => dateTimeOffset.ToUnixTimeSeconds(),
            TimeStampType.Javascript => dateTimeOffset.ToUnixTimeMilliseconds(),
            _ => throw new ArgumentOutOfRangeException(nameof(timeStampType), timeStampType, null)
        };
    }
    /// <summary>
    /// Convert to the representation of the day of the week in Chinese style (Sunday is the seventh day)
    /// </summary>
    /// <param name="week"></param>
    /// <returns>1-7 corresponds to Monday to Sunday</returns>
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
        new(time.Ticks - time.Ticks % TimeSpan.TicksPerSecond, time.Kind);

    /// <summary>
    /// Combine given and time from given DateTime.
    /// </summary>
    /// <param name="date"></param>
    /// <param name="time"></param>
    /// <returns></returns>
    public static DateTime CombineDateAndTime(this DateTime date, DateTime time) =>
        date.Date.Add(time.TimeOfDay);

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
            DateTimePart.Year => new DateTime(dateTime.Year, 1, 1, 0, 0, 0, dateTime.Kind),
            DateTimePart.Month => new DateTime(dateTime.Year, dateTime.Month, 1, 0, 0, 0, dateTime.Kind),
            DateTimePart.Day => dateTime.Date,
            DateTimePart.Hour => new DateTime(dateTime.Ticks - dateTime.Ticks % TimeSpan.TicksPerHour, dateTime.Kind),
            DateTimePart.Minute => new DateTime(dateTime.Ticks - dateTime.Ticks % TimeSpan.TicksPerMinute, dateTime.Kind),
            DateTimePart.Second => new DateTime(dateTime.Ticks - dateTime.Ticks % TimeSpan.TicksPerSecond, dateTime.Kind),
            DateTimePart.Millisecond => new DateTime(dateTime.Ticks - dateTime.Ticks % TimeSpan.TicksPerMillisecond, dateTime.Kind),
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, null)
        };
    }
}
