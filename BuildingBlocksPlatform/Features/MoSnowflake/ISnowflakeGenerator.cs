namespace BuildingBlocksPlatform.Features.MoSnowflake;

public interface ISnowflakeGenerator
{
    /// <summary>
    /// 获取一个雪花ID
    /// </summary>
    /// <returns></returns>
    public long GenerateSnowflakeId();
}


public class SnowflakeStatic
{
    public static ISnowflakeGenerator Snowflake { get; set; } = null!;
}