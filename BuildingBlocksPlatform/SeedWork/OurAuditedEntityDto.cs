using BuildingBlocksPlatform.Repository.DtoInterfaces;


namespace BuildingBlocksPlatform.SeedWork;
public abstract class OurAuditedEntityDto : OurAuditedEntityDto<long>
{

}
public abstract class OurAuditedEntityDto<TPrimaryKey> : MoFullAuditedEntityDto<TPrimaryKey>, IHasEntityId<TPrimaryKey>
{
 
}

public interface IHasEntityId<TKey> : IMoEntityDto<TKey>
{
  
}

public abstract class OurEntityDto<TPrimaryKey> : MoEntityDto<TPrimaryKey>, IHasEntityId<TPrimaryKey>
{
    
}
