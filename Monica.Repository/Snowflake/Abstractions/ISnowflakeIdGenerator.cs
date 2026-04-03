namespace Monica.Core.Features.MoSnowflake;

/// <summary>
/// Generates distributed identifiers using the Snowflake algorithm.
/// </summary>
public interface ISnowflakeIdGenerator
{
    /// <summary>
    /// Generates the next identifier.
    /// </summary>
    /// <returns>The generated identifier.</returns>
    long GenerateId();
}
