using System;

namespace MoLibrary.Tool.Extensions
{
    /// <summary>
    /// Extension methods of Time related type
    /// </summary>
    public static class TimeExtensions
    {
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
        public static ChineseWeeks ToChineseWeek(this DayOfWeek week) => week == DayOfWeek.Sunday ? ChineseWeeks.星期日 : (ChineseWeeks)week;

        #endregion
        /// <summary>
        /// Chinese week.
        /// </summary>
        public enum ChineseWeeks
        {
            星期一 = 1,
            星期二 = 2,
            星期三 = 3,
            星期四 = 4,
            星期五 = 5,
            星期六 = 6,
            星期日 = 7,
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
}