using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Features.MoClock;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Tool.Extensions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;


public static class ModuleClockBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the Clock module.
        /// </summary>
        public static ModuleClockGuide AddClock(Action<ModuleClockOption>? action = null)
        {
            return new ModuleClockGuide().Register(action);
        }
    }
}

public class ModuleClock(ModuleClockOption option) : MoModule<ModuleClock, ModuleClockOption, ModuleClockGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Clock;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        var targetTimeZone = Option.ConfiguredTimeZone ?? TimeZoneInfo.Local;
        TimeExtensions.LocalTimeZoneInfo = targetTimeZone;

        Logger.LogWarning(
            "Clock module timezone set to: {TimeZoneId} (Display: {DisplayName}, Offset: {BaseUtcOffset})",
            targetTimeZone.Id,
            targetTimeZone.DisplayName,
            targetTimeZone.BaseUtcOffset);
        Logger.LogWarning("Current system local timezone(TimeZoneInfo.Local): {timezone}", TimeZoneInfo.Local);
        
    }
}

public class ModuleClockGuide : MoModuleGuide<ModuleClock, ModuleClockOption, ModuleClockGuide>
{
    /// <summary>
    /// Sets the application timezone using a TimeZoneInfo ID string.
    /// </summary>
    /// <param name="timeZoneId">The TimeZoneInfo ID (e.g., "China Standard Time", "Eastern Standard Time")</param>
    /// <returns>The guide instance for method chaining</returns>
    /// <exception cref="ArgumentException">Thrown when the specified timezone ID is not found or is invalid</exception>
    public ModuleClockGuide SetTimeZone(string timeZoneId)
    {
        ConfigureModuleOption(option =>
        {
            try
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                option.ConfiguredTimeZone = timeZone;
            }
            catch (TimeZoneNotFoundException ex)
            {
                Logger.LogError(ex, "Invalid timezone ID: {TimeZoneId}. Please use a valid TimeZoneInfo ID.", timeZoneId);
                throw new ArgumentException($"Invalid timezone ID: {timeZoneId}. The specified timezone was not found on this system.", nameof(timeZoneId), ex);
            }
            catch (InvalidTimeZoneException ex)
            {
                Logger.LogError(ex, "Invalid timezone data for ID: {TimeZoneId}", timeZoneId);
                throw new ArgumentException($"Invalid timezone data for ID: {timeZoneId}.", nameof(timeZoneId), ex);
            }
        });

        return this;
    }

    /// <summary>
    /// Sets the application timezone using a common timezone enum.
    /// </summary>
    /// <param name="commonTimeZone">The common timezone enum value</param>
    /// <returns>The guide instance for method chaining</returns>
    /// <exception cref="InvalidOperationException">Thrown when the timezone is not available on the system</exception>
    public ModuleClockGuide SetTimeZone(ECommonTimeZones commonTimeZone)
    {
        ConfigureModuleOption(option =>
        {
            try
            {
                var timeZone = commonTimeZone.GetTimeZoneInfo();
                option.ConfiguredTimeZone = timeZone;
                Logger.LogDebug("Clock module timezone set to {CommonTimeZone}: {TimeZoneId}",
                    commonTimeZone, timeZone.Id);
            }
            catch (TimeZoneNotFoundException ex)
            {
                Logger.LogError(ex, "Timezone not found for {CommonTimeZone}", commonTimeZone);
                throw new InvalidOperationException($"Failed to set timezone for {commonTimeZone}. The timezone is not available on this system.", ex);
            }
        });

        return this;
    }

    /// <summary>
    /// Sets the application timezone using a TimeZoneInfo object directly.
    /// </summary>
    /// <param name="timeZoneInfo">The TimeZoneInfo object</param>
    /// <returns>The guide instance for method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when timeZoneInfo is null</exception>
    public ModuleClockGuide SetTimeZone(TimeZoneInfo timeZoneInfo)
    {
        ArgumentNullException.ThrowIfNull(timeZoneInfo);

        ConfigureModuleOption(option =>
        {
            option.ConfiguredTimeZone = timeZoneInfo;
        });

        return this;
    }
}

public class ModuleClockOption : MoModuleOption<ModuleClock>
{
    /// <summary>
    /// The configured timezone for the application.
    /// If null, the system's local timezone will be used.
    /// </summary>
    public TimeZoneInfo? ConfiguredTimeZone { get; internal set; }
}
