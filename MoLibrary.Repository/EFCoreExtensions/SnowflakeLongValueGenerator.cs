using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using MoLibrary.Core.Features.MoSnowflake;

namespace MoLibrary.Repository.EFCoreExtensions;

public class SnowflakeLongValueGenerator : ValueGenerator<long>
{
    public override long Next(EntityEntry entry)
    {
        var snowflake = entry.Context.GetService<ISnowflakeGenerator>();
        return snowflake.GenerateSnowflakeId();
    }

    public override bool GeneratesTemporaryValues => false;
}