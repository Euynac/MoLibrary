using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Monica.Repository.Snowflake.Abstractions;

namespace Monica.Repository.EFCoreExtensions;

public class SnowflakeLongValueGenerator : ValueGenerator<long>
{
    public override long Next(EntityEntry entry)
    {
        var snowflake = entry.Context.GetService<ISnowflakeIdGenerator>();
        return snowflake.GenerateId();
    }

    public override bool GeneratesTemporaryValues => false;
}