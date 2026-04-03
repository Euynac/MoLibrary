namespace Monica.Core.Clock;

/// <summary>
/// Extension methods for <see cref="CommonTimeZone" />.
/// </summary>
public static class CommonTimeZoneExtensions
{
    /// <summary>
    /// Gets the TimeZoneInfo ID for the specified common timezone.
    /// </summary>
    /// <param name="timezone">The common timezone enum value.</param>
    /// <returns>The Windows TimeZoneInfo ID string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an unknown timezone value is provided.</exception>
    public static string GetTimeZoneId(this CommonTimeZone timezone)
    {
        return timezone switch
        {
            CommonTimeZone.China => "China Standard Time",
            CommonTimeZone.USEastern => "Eastern Standard Time",
            CommonTimeZone.USPacific => "Pacific Standard Time",
            CommonTimeZone.USCentral => "Central Standard Time",
            CommonTimeZone.USMountain => "Mountain Standard Time",
            CommonTimeZone.Japan => "Tokyo Standard Time",
            CommonTimeZone.UnitedKingdom => "GMT Standard Time",
            CommonTimeZone.EUCentral => "Central European Standard Time",
            CommonTimeZone.AustraliaEast => "AUS Eastern Standard Time",
            CommonTimeZone.India => "India Standard Time",
            CommonTimeZone.Singapore => "Singapore Standard Time",
            CommonTimeZone.Korea => "Korea Standard Time",
            CommonTimeZone.Dubai => "Arabian Standard Time",
            CommonTimeZone.SouthAfrica => "South Africa Standard Time",
            CommonTimeZone.Brazil => "E. South America Standard Time",
            _ => throw new ArgumentOutOfRangeException(nameof(timezone), timezone, null)
        };
    }

    /// <summary>
    /// Gets the <see cref="TimeZoneInfo" /> object for the specified common timezone.
    /// </summary>
    /// <param name="timezone">The common timezone enum value.</param>
    /// <returns>The <see cref="TimeZoneInfo" /> object for the specified timezone.</returns>
    /// <exception cref="TimeZoneNotFoundException">Thrown when the timezone is not found on the system.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an unknown timezone value is provided.</exception>
    public static TimeZoneInfo GetTimeZoneInfo(this CommonTimeZone timezone)
    {
        return TimeZoneInfo.FindSystemTimeZoneById(timezone.GetTimeZoneId());
    }
}
