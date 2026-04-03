namespace Monica.Core.Features.MoSnowflake;

/// <summary>
/// Temporary compatibility bridge for legacy code paths that still resolve Snowflake generation through a static entry point.
/// Prefer injecting <see cref="ISnowflakeIdGenerator" /> in new code.
/// </summary>
public static class SnowflakeStatic
{
    /// <summary>
    /// Gets or sets the process-wide Snowflake generator instance used by legacy static callers.
    /// </summary>
    public static ISnowflakeIdGenerator Snowflake { get; set; } = null!;
}
