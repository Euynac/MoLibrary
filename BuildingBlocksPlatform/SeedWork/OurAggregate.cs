using System.Text.Json.Serialization;
using BuildingBlocksPlatform.Features.MoSnowflake;
using BuildingBlocksPlatform.Repository.EntityInterfaces;


namespace BuildingBlocksPlatform.SeedWork;


public abstract class OurAggregate<TKey> : MoEntity<TKey>, IOurEntity<TKey>, IHasConcurrencyStamp
{
    [JsonIgnore]
    public string ConcurrencyStamp { get; set; } = null!;
    protected OurAggregate()
    {
    }

    protected OurAggregate(TKey id)
    {
    }
}
public class OurAggregate : OurAggregate<long>
{
    protected OurAggregate()
    {
    }

    public override void AutoSetNewId(bool notSetWhenNotDefault = false)
    {
        SetNewId(SnowflakeStatic.Snowflake.GenerateSnowflakeId());
    }
}

public abstract class OurFullAuditedAggregate<TKey> : MoFullAuditedEntity<TKey>, IOurEntity<TKey>, IHasConcurrencyStamp
{
    [JsonIgnore]
    public string ConcurrencyStamp { get; set; } = null!;

    protected OurFullAuditedAggregate()
    {

    }
}

public class OurFullAuditedAggregate : OurFullAuditedAggregate<long> 
{
    public override void AutoSetNewId(bool notSetWhenNotDefault = false)
    {
        SetNewId(SnowflakeStatic.Snowflake.GenerateSnowflakeId());
    }
    protected OurFullAuditedAggregate()
    {

    }
}