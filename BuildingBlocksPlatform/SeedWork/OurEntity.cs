using MoLibrary.Core.Features.MoSnowflake;
using MoLibrary.Repository.EntityInterfaces;

namespace BuildingBlocksPlatform.SeedWork;

/// <summary>
/// 标志是实体类
/// </summary>
public interface IOurEntity : IMoEntity
{
}

public interface IOurEntity<TKey> : IMoEntity<TKey>, IOurEntity
{
   
}

/// <summary>
/// 雪花Id型实体 
/// </summary>
public class OurEntity : OurEntity<long>
{
    public override void AutoSetNewId(bool notSetWhenNotDefault = false)
    {
        if (notSetWhenNotDefault && Id != default) return;
        SetNewId(SnowflakeStatic.Snowflake.GenerateSnowflakeId());
    }
}

public abstract class OurEntity<TKey> : MoEntity<TKey> ,IOurEntity<TKey>
{
    public override void AutoSetNewId(bool notSetWhenNotDefault = false)
    {
        if (notSetWhenNotDefault && Id is long) return;
        if (Id is long)
        {
            SetNewId((dynamic) SnowflakeStatic.Snowflake.GenerateSnowflakeId());
        }
        else
        {
            base.AutoSetNewId(notSetWhenNotDefault);
        }
    }
}

public abstract class OurFullAuditedEntity<TKey> : MoFullAuditedEntity<TKey>, IOurEntity<TKey>
{
    public override void AutoSetNewId(bool notSetWhenNotDefault = false)
    {
        if (notSetWhenNotDefault && Id is long) return;
        if (Id is long)
        {
            SetNewId((dynamic) SnowflakeStatic.Snowflake.GenerateSnowflakeId());
        }
        else
        {
            base.AutoSetNewId(notSetWhenNotDefault);
        }
    }
}
public class OurFullAuditedEntity : OurFullAuditedEntity<long>
{
    public override void AutoSetNewId(bool notSetWhenNotDefault = false)
    {
        SetNewId(SnowflakeStatic.Snowflake.GenerateSnowflakeId());
    }
}