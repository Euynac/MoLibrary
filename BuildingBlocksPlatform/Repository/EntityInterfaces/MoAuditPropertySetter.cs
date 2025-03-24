using BuildingBlocksPlatform.Repository.EntityInterfaces.Auditing;
using MoLibrary.Authority.Security;

namespace BuildingBlocksPlatform.Repository.EntityInterfaces;
public class MoAuditPropertySetter(IMoCurrentUser currentUser) : IMoAuditPropertySetter
{
    protected IMoCurrentUser CurrentUser { get; } = currentUser;

    public virtual void SetCreationProperties(object targetObject)
    {
        SetCreationTime(targetObject);
        SetCreatorId(targetObject);
    }

    public virtual void SetModificationProperties(object targetObject)
    {
        SetLastModificationTime(targetObject);
        SetLastModifierId(targetObject);
    }

    public virtual void SetDeletionProperties(object targetObject)
    {
        SetDeletionTime(targetObject);
        SetDeleterId(targetObject);
    }

    public virtual void IncrementEntityVersionProperty(object targetObject)
    {
        if (targetObject is IHasEntityVersion objectWithEntityVersion)
        {
            ObjectHelper.TrySetProperty(objectWithEntityVersion, x => x.EntityVersion, x => x.EntityVersion + 1);
        }
    }

    protected virtual void SetCreationTime(object targetObject)
    {
        if (targetObject is not IHasCreationTime objectWithCreationTime)
        {
            return;
        }

        if (objectWithCreationTime.CreationTime == default)
        {
            ObjectHelper.TrySetProperty(objectWithCreationTime, x => x.CreationTime, () => DateTime.Now);
        }
    }


    protected virtual void SetLastModificationTime(object targetObject)
    {
        if (targetObject is IHasModificationTime objectWithModificationTime)
        {
            ObjectHelper.TrySetProperty(objectWithModificationTime, x => x.LastModificationTime, () => DateTime.Now);
        }
        if (targetObject is IHasModificationTime objectWithModificationTime2)
        {
            ObjectHelper.TrySetProperty(objectWithModificationTime2, x => x.LastModificationTime, () => DateTime.Now);
        }
    }

 
    protected virtual void SetDeletionTime(object targetObject)
    {
        if (targetObject is IHasDeletionTime {DeletionTime: null} objectWithDeletionTime)
        {
            ObjectHelper.TrySetProperty(objectWithDeletionTime, x => x.DeletionTime, () => DateTime.Now);
        }
    }
    protected virtual void SetLastModifierId(object targetObject)
    {
        if (targetObject is not IHasLastModifier modificationAuditedObject)
        {
            return;
        }

        if (targetObject is IHasLastModifierName modifier)
        {
            ObjectHelper.TrySetProperty(modifier, x => x.LastModifier, () => CurrentUser.Username);
        }
        
        ObjectHelper.TrySetProperty(modificationAuditedObject, x => x.LastModifierId, () => CurrentUser.Id);
    }

    protected virtual void SetCreatorId(object targetObject)
    {
        if (targetObject is not IHasCreator creatorObject) return;
        
        if (!string.IsNullOrEmpty(creatorObject.CreatorId))
        {
            return;
        }
            
        if(targetObject is IHasCreatorName  objectWithCreatorName)
        {
            ObjectHelper.TrySetProperty(objectWithCreatorName, x => x.Creator, () => CurrentUser.Username);
        }
        ObjectHelper.TrySetProperty(creatorObject, x => x.CreatorId, () => CurrentUser.Id);
    }
    protected virtual void SetDeleterId(object targetObject)
    {
        if (targetObject is not IHasDeleter deletionAuditedObject)
        {
            return;
        }

        if (!string.IsNullOrEmpty(deletionAuditedObject.DeleterId))
        {
            return;
        }
        //巨坑：不能直接改，否则会报：A second operation was started on this context instance before a previous operation completed. This is usually caused by different threads concurrently using the same instance of DbContext. For more information on how to avoid threading issues with DbContext, see https://go.microsoft.com/fwlink/?linkid=2097913."
        if (targetObject is IHasDeleterName deleter)
        {
            ObjectHelper.TrySetProperty(deleter, x => x.Deleter, () => CurrentUser.Username);
        }

        ObjectHelper.TrySetProperty(deletionAuditedObject, x => x.DeleterId, () => CurrentUser.Id);
    }
}