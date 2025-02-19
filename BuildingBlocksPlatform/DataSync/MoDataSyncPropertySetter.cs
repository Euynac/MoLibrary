using BuildingBlocksPlatform.DataSync.Interfaces;
using BuildingBlocksPlatform.Repository.EntityInterfaces;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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

            ObjectHelper.TrySetProperty(target, x => x.DataSyncFlags, (x) => x.DataSyncFlags | ESystemDataSpecialFlags.SelfHosted);
            ObjectHelper.TrySetProperty(target, x => x.DataSyncSource, dataSyncFunc.GetLocalAddress);
        }
    }

}