namespace Monica.Core.Features.MoSnowflake;

public interface ISnowflakeGenerator
{
    /// <summary>
    /// Generates a Snowflake identifier.
    /// </summary>
    /// <returns>The generated identifier.</returns>
    public long GenerateSnowflakeId();
}


public class SnowflakeStatic
{
    public static ISnowflakeGenerator Snowflake { get; set; } = null!;
}
