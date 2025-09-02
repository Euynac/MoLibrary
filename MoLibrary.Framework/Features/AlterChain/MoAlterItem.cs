using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoLibrary.Framework.Features.AlterChain;


public interface IMoAlterItem
{
    public static readonly JsonSerializerOptions ALTER_ITEM_SERIALIZER_OPTIONS = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public class MoAlterItem<TTargetEntity, TAlterItemData, TEnumAlterSource> : IMoAlterItem where TAlterItemData : class, IMoTracingDataAlterItemData<TTargetEntity>, new() where TTargetEntity : IMoTracingDataEntity where TEnumAlterSource:Enum
{
    /// <summary>
    /// 变更项ID
    /// </summary>
    public string Id { get; set; }


    #region 变更项

    public string DataJson { get; set; } = null!;
    private TAlterItemData? _data;


    [NotMapped]
    [JsonIgnore]
    public TAlterItemData Data
    {
        get => _data ??= JsonSerializer.Deserialize<TAlterItemData>(DataJson)!;
        set
        {
            _data = value;
            RefreshDataJson();
        }
    }

    /// <summary>
    /// 刷新DataJson，在Data发生变更时必须调用，否则不会生效
    /// </summary>
    public void RefreshDataJson()
    {
        DataJson = JsonSerializer.Serialize(_data, IMoAlterItem.ALTER_ITEM_SERIALIZER_OPTIONS);
    }

    #endregion

    #region 回滚操作
    /// <summary>
    /// 目标回滚ID
    /// </summary>
    public List<string>? TargetRollbackIds { get; set; }

    /// <summary>
    /// 是否已经回滚(无效化标志位)
    /// </summary>
    public bool IsInvalid => RollbackBy is not null;

    /// <summary>
    /// (有值则认为已被回滚) 使得该修改项回滚的回滚修改项ID
    /// </summary>
    public string? RollbackBy { get; set; }

    public void DoRollback(string rollbackBy)
    {
        RollbackBy = rollbackBy;
    }

    public void CancelRollback()
    {
        RollbackBy = null;
    }

    #endregion

    #region Setting
    /// <summary>
    /// 该次变更操作时间
    /// </summary>
    public DateTime AlterTime { get; set; }

    /// <summary>
    /// 实际变更操作影响时间，默认是AlterTime
    /// </summary>
    public DateTime? ActualEffectTime { get; set; }

    /// <summary>
    /// 排序时使用时间，也是真正应用到修改的时间
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public DateTime OrderTime => (ActualEffectTime ?? AlterTime).AddMicroseconds(OffsetAdjust);

    /// <summary>
    /// 微调修改时间，用于解决变更操作时间重复等极端情况
    /// </summary>
    public int OffsetAdjust { get; set; }

    /// <summary>
    /// 变更备注
    /// </summary>
    public string? Remarks { get; set; }
    /// <summary>
    /// 变更来源
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TEnumAlterSource Source { get; set; }
    /// <summary>
    /// 变更来源名
    /// </summary>
    public string? SourceInfo { get; set; }
    /// <summary>
    /// 变更来源GUID，如用户GUID等
    /// </summary>
    public string? SourceId { get; set; }

    /// <summary>
    /// 变更相关操作来源组ID，可用于按影响组回退
    /// </summary>
    public List<string>? RelatedSourceId { get; set; }
    #endregion

    public MoAlterItem(string id, TEnumAlterSource source, DateTime alterTime, TAlterItemData data, string? sourceId, string? sourceInfo = null)
    {
        Id = id;
        Source = source;
        AlterTime = alterTime;
        DataJson = null!;
        Data = data;
        SourceId = sourceId;
        SourceInfo = sourceInfo;
    }

    public MoAlterItem(string id, TEnumAlterSource source, DateTime alterTime, List<string> targetRollbackIds, string? sourceId, string? sourceInfo = null)
        : this(id, source, alterTime, new TAlterItemData(), sourceId, sourceInfo)
    {
        TargetRollbackIds = targetRollbackIds;
    }

    protected MoAlterItem()
    {
        Id = "";
    }

    /// <summary>
    /// 应用修改
    /// </summary>
    public TTargetEntity ApplyAlter(TTargetEntity entity)
    {
        if (IsInvalid || TargetRollbackIds is not null) return entity;
        Data.Apply(entity);
        return entity;
    }
}