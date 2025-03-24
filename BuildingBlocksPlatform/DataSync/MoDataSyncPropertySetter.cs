using BuildingBlocksPlatform.DataSync.Interfaces;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.Repository.EntityInterfaces;

namespace BuildingBlocksPlatform.DataSync;

public class MoDataSyncPropertySetter(IDataSyncFunctions dataSyncFunc) : IMoDataSyncPropertySetter, ITransientDependency
{
    /// <summary>
    /// 仓储层自动设置与自管相关的值
    /// </summary>
    /// <param name="targetObject"></param>
    public void SetDataSyncProperties(object targetObject)
    {
        if (targetObject is ISystemEntityDataSync target && dataSyncFunc.IsSelfHostOperation() && !target.IsSelfHost())
        {
            //ObjectHelper.TrySetProperty(dataSyncObject, x => x.DataSyncFlags, () => CurrentUser.Username);
            // TODO
            var flags = target.DataSyncFlags;
            if (dataSyncFunc.IsSelfHostOperation())
            {
                flags = flags | ESystemDataSpecialFlags.SelfHosted;
            }

            if (dataSyncFunc.IsUploading())
            {
                flags = flags | ESystemDataSpecialFlags.Uploading;
            }

            ObjectHelper.TrySetProperty(target, x => x.DataSyncFlags, (x) => flags);
            ObjectHelper.TrySetProperty(target, x => x.DataSyncSource, dataSyncFunc.GetLocalAddress);
        }
    }

}