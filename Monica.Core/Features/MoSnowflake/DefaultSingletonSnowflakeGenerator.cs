using Monica.Core.Modules;

namespace Monica.Core.Features.MoSnowflake;

public class DefaultSingletonSnowflakeGenerator(ModuleSnowflakeIdOption configuration) : ISnowflakeGenerator
{
    private readonly Snowflake _snowflake = new(configuration);

    public long GenerateSnowflakeId()
    {
        return _snowflake.NextId();
    }
}