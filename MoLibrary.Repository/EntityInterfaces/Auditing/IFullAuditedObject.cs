namespace MoLibrary.Repository.EntityInterfaces.Auditing;

public interface IFullAuditedObject : IHasCreationTime, IHasModificationTime, IHasDeletionTime, IHasCreator, IHasDeleter, IHasLastModifier
{
}
