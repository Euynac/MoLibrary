namespace BuildingBlocksPlatform.Repository.EntityInterfaces;

public interface IMoAuditPropertySetter
{
    void SetCreationProperties(object targetObject);

    void SetModificationProperties(object targetObject);

    void SetDeletionProperties(object targetObject);

    void IncrementEntityVersionProperty(object targetObject);
}
