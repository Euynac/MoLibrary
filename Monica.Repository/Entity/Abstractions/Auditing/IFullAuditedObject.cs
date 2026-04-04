namespace Monica.Repository.Entity.Abstractions.Auditing;

public interface IFullAuditedObject : IHasCreationTime, IHasModificationTime, IHasDeletionTime, IHasCreator, IHasDeleter, IHasLastModifier
{
}
