namespace BuildingBlocksPlatform.Features.MoSnowflake;

public class DefaultSingletonSnowflakeGenerator(SnowflakeConfiguration configuration) : ISnowflakeGenerator
{
    private readonly Snowflake _snowflake = new(configuration);

    public long GenerateSnowflakeId()
    {
        return _snowflake.NextId();
    }
}