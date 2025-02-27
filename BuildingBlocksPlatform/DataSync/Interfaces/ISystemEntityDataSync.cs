using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildingBlocksPlatform.DataSync.Interfaces;

/// <summary>
/// 数据同步特殊标志位
/// </summary>
[Flags]
public enum ESystemDataSpecialFlags
{
    /// <summary>
    /// 无特殊标志
    /// </summary>
    None = 0,
    /// <summary>
    /// 需要进行一二级同步，这些数据一旦收到同步事件直接忽略处理。这些一般出现在二级切换为独立运行模式的情况，此情况下，二级节点自行修改的数据将均打上自管标志位，若一级节点恢复，则这些数据将不会进行同步，需手动选择是否恢复为普通数据，使用一级数据覆盖（手动同步）。
    /// </summary>
    SelfHosted = 1 << 0,
    ///// <summary>
    ///// 自管数据由人工标记为待上传，需要上传一级进行管理
    ///// </summary>
    Uploading = 1 << 1,
    /// <summary>
    /// 这部分数据已经成功上传一级，系统将状态由待上传改为已上传（二级将收到来自一级数据标记返回）
    /// </summary>
    Uploaded = 1 << 2,
    ///// <summary>
    ///// 数据待审核
    ///// </summary>
    UnderReview = 1 << 3,
    /// <summary>
    /// 数据已审核, 
    /// </summary>
    Reviewed = 1 << 4,

    /// <summary>
    /// 二级主动取消
    /// </summary>
    Cancel = 1 << 5,

    /// <summary>
    /// 审核未通过
    /// </summary>
    Rejected = 1 << 6,
}

/// <summary>
/// 指示该实体一二级同步设置
/// </summary>
public interface ISystemEntityDataSync
{
    /// <summary>
    /// 自管数据来源
    /// </summary>
    public string? DataSyncSource { get; set; }

    /// <summary>
    /// 数据同步特殊功能标志位
    /// </summary>
    public ESystemDataSpecialFlags DataSyncFlags { get; set; }

    /// <summary>
    /// 是自管数据
    /// </summary>
    /// <returns></returns>
    public bool IsSelfHost()
    {
        return DataSyncFlags.HasFlag(ESystemDataSpecialFlags.SelfHosted);
    }

    /// <summary>
    /// 设置为自管数据
    /// </summary>
    /// <param name="source"></param>
    public void SetAsSelfHost(string source)
    {
        DataSyncFlags |= ESystemDataSpecialFlags.SelfHosted;
        DataSyncSource = source;
    }

    public void SetState(ESystemDataSpecialFlags state)
    {
        if (state == ESystemDataSpecialFlags.Uploading ||
            state == ESystemDataSpecialFlags.Uploaded ||
            state == ESystemDataSpecialFlags.Cancel ||
            state == ESystemDataSpecialFlags.Rejected ||
            state == ESystemDataSpecialFlags.Reviewed || 
            state == ESystemDataSpecialFlags.UnderReview)
        {
            // 重置状态
            DataSyncFlags = DataSyncFlags.Remove(ESystemDataSpecialFlags.Uploading, ESystemDataSpecialFlags.Uploaded, ESystemDataSpecialFlags.Cancel,
                ESystemDataSpecialFlags.Rejected, ESystemDataSpecialFlags.Reviewed, ESystemDataSpecialFlags.UnderReview);
        }

        DataSyncFlags |= state;
    }
}

