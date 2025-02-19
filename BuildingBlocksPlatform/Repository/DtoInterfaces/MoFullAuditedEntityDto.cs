using BuildingBlocksPlatform.Repository.EntityInterfaces.Auditing;

namespace BuildingBlocksPlatform.Repository.DtoInterfaces;

/// <summary>
/// This class can be inherited by DTO classes to implement <see cref="IFullAuditedObject"/> interface.
/// </summary>
[Serializable]
public abstract class MoFullAuditedEntityDto : MoEntityDto, IFullAuditedObject
{
    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletionTime { get; set; }
    public string? CreatorId { get; set; }
    public string? DeleterId { get; set; }
    public string? LastModifierId { get; set; }
}

/// <summary>
/// This class can be inherited by DTO classes to implement <see cref="IFullAuditedObject"/> interface.
/// </summary>
/// <typeparam name="TPrimaryKey">Type of primary key</typeparam>
[Serializable]
public abstract class MoFullAuditedEntityDto<TPrimaryKey> : MoEntityDto<TPrimaryKey>, IFullAuditedObject
{
    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletionTime { get; set; }
    public string? CreatorId { get; set; }
    public string? DeleterId { get; set; }
    public string? LastModifierId { get; set; }
}
