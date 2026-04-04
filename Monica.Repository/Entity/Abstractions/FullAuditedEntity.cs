using Monica.Repository.Entity.Abstractions.Auditing;

namespace Monica.Repository.Entity.Abstractions;

/// <summary>
/// This class can be inherited by  classes to implement <see cref="IFullAuditedObject"/> interface.
/// </summary>
[Serializable]
public abstract class FullAuditedEntity : Entity, IFullAuditedObject
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
/// This class can be inherited by  classes to implement <see cref="IFullAuditedObject"/> interface.
/// </summary>
/// <typeparam name="TPrimaryKey">Type of primary key</typeparam>
[Serializable]
public abstract class FullAuditedEntity<TPrimaryKey> : Entity<TPrimaryKey>, IFullAuditedObject
{
    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletionTime { get; set; }
    public string? CreatorId { get; set; }
    public string? DeleterId { get; set; }
    public string? LastModifierId { get; set; }
}
