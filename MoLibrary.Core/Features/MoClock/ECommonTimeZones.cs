using System;

namespace MoLibrary.Core.Features.MoClock;

/// <summary>
/// Common timezone identifiers for major countries and regions.
/// These IDs are cross-platform compatible (.NET 6+).
/// </summary>
public enum ECommonTimeZones
{
    /// <summary>
    /// China Standard Time (UTC+8)
    /// </summary>
    China,

    /// <summary>
    /// Eastern Standard Time - US &amp; Canada (UTC-5/UTC-4)
    /// </summary>
    USEastern,

    /// <summary>
    /// Pacific Standard Time - US &amp; Canada (UTC-8/UTC-7)
    /// </summary>
    USPacific,

    /// <summary>
    /// Central Standard Time - US &amp; Canada (UTC-6/UTC-5)
    /// </summary>
    USCentral,

    /// <summary>
    /// Mountain Standard Time - US &amp; Canada (UTC-7/UTC-6)
    /// </summary>
    USMountain,

    /// <summary>
    /// Japan Standard Time (UTC+9)
    /// </summary>
    Japan,

    /// <summary>
    /// GMT Standard Time - United Kingdom (UTC+0/UTC+1)
    /// </summary>
    UnitedKingdom,

    /// <summary>
    /// Central European Standard Time - EU (UTC+1/UTC+2)
    /// </summary>
    EUCentral,

    /// <summary>
    /// AUS Eastern Standard Time - Australia East (UTC+10/UTC+11)
    /// </summary>
    AustraliaEast,

    /// <summary>
    /// India Standard Time (UTC+5:30)
    /// </summary>
    India,

    /// <summary>
    /// Singapore Standard Time (UTC+8)
    /// </summary>
    Singapore,

    /// <summary>
    /// Korea Standard Time (UTC+9)
    /// </summary>
    Korea,

    /// <summary>
    /// Arabian Standard Time - Dubai, Abu Dhabi (UTC+4)
    /// </summary>
    Dubai,

    /// <summary>
    /// South Africa Standard Time (UTC+2)
    /// </summary>
    SouthAfrica,

    /// <summary>
    /// Brazil East Standard Time - Brasilia (UTC-3)
    /// </summary>
    Brazil
}

/// <summary>
/// Extension methods for ECommonTimeZones enum
/// </summary>
public static class ECommonTimeZonesExtensions
{
    /// <summary>
    /// Gets the TimeZoneInfo ID for the specified common timezone
    /// </summary>
    /// <param name="timezone">The common timezone enum value</param>
    /// <returns>The Windows TimeZoneInfo ID string</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an unknown timezone value is provided</exception>
    public static string GetTimeZoneId(this ECommonTimeZones timezone)
    {
        return timezone switch
        {
            ECommonTimeZones.China => "China Standard Time",
            ECommonTimeZones.USEastern => "Eastern Standard Time",
            ECommonTimeZones.USPacific => "Pacific Standard Time",
            ECommonTimeZones.USCentral => "Central Standard Time",
            ECommonTimeZones.USMountain => "Mountain Standard Time",
            ECommonTimeZones.Japan => "Tokyo Standard Time",
            ECommonTimeZones.UnitedKingdom => "GMT Standard Time",
            ECommonTimeZones.EUCentral => "Central European Standard Time",
            ECommonTimeZones.AustraliaEast => "AUS Eastern Standard Time",
            ECommonTimeZones.India => "India Standard Time",
            ECommonTimeZones.Singapore => "Singapore Standard Time",
            ECommonTimeZones.Korea => "Korea Standard Time",
            ECommonTimeZones.Dubai => "Arabian Standard Time",
            ECommonTimeZones.SouthAfrica => "South Africa Standard Time",
            ECommonTimeZones.Brazil => "E. South America Standard Time",
            _ => throw new ArgumentOutOfRangeException(nameof(timezone), timezone, null)
        };
    }

    /// <summary>
    /// Gets the TimeZoneInfo object for the specified common timezone
    /// </summary>
    /// <param name="timezone">The common timezone enum value</param>
    /// <returns>The TimeZoneInfo object for the specified timezone</returns>
    /// <exception cref="TimeZoneNotFoundException">Thrown when the timezone is not found on the system</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an unknown timezone value is provided</exception>
    public static TimeZoneInfo GetTimeZoneInfo(this ECommonTimeZones timezone)
    {
        return TimeZoneInfo.FindSystemTimeZoneById(timezone.GetTimeZoneId());
    }
}
