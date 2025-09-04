using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Features.AlterChain;


public interface IMoAlterItem
{
    static readonly JsonSerializerOptions ALTER_ITEM_SERIALIZER_OPTIONS = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}

public class MoAlterItem<TTargetEntity, TAlterItemData, TEnumAlterSource> : IMoAlterItem where TAlterItemData : class, IMoTracingDataAlterItemData<TTargetEntity>, new() where TTargetEntity : class, IMoTracingDataEntity where TEnumAlterSource:Enum
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
        get => _data ??= JsonSerializer.Deserialize<TAlterItemData>(DataJson, IMoAlterItem.ALTER_ITEM_SERIALIZER_OPTIONS)!;
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
    
    /// <summary>
    /// 获取原状态到应用此变更的变更描述
    /// </summary>
    /// <param name="previousStatus"></param>
    /// <returns></returns>
    public List<AlterRecord> GetChangesFromPreviousStatus(TTargetEntity previousStatus)
    {
        var list = new List<AlterRecord>();
        if (TargetRollbackIds is not null)
        {
            list.AddIfNotNull(new AlterRecord()
            {
                IsRollback = IsInvalid,
                DisplayName = "回滚修改",
                TargetRollbackIds = TargetRollbackIds
            });
        }
        else
        {
            foreach (var propertyAlterData in Data.GetChanges(previousStatus))
            {
                list.AddIfNotNull(Format(propertyAlterData.DisplayName, propertyAlterData.OldValue, propertyAlterData.NewValue));
            }
            
            static AlterRecord? Format(string displayName, object? oldValue, object? newValue)
            {
                if (newValue == null || oldValue == newValue)
                {
                    return null;
                }

                if (oldValue is not null)
                {
                    if (oldValue is string originStr && newValue is string newStr && originStr == newStr)
                    {
                        return null;
                    }
                    if (oldValue is DateTime originDateTime && newValue is DateTime newDateTime && originDateTime.EqualBySecond(newDateTime))
                    {
                        return null;
                    }

                    if (oldValue is TimeSpan originTimeSpan && newValue is TimeSpan newTimeSpan && originTimeSpan == newTimeSpan)
                    {
                        return null;
                    }

                    if (oldValue is bool originBool && newValue is bool newBool && originBool == newBool)
                    {
                        return null;
                    }

                    if (oldValue is int originInt && newValue is int newInt && originInt == newInt)
                    {
                        return null;
                    }
                }


                if (oldValue is Enum oriEnum && newValue is Enum newEnum)
                {
                    return new AlterRecord()
                    {
                        DisplayName = displayName,
                        OldValue = oriEnum.ToString(),
                        NewValue = newEnum.ToString()
                    };
                }
                var record = new AlterRecord()
                {
                    DisplayName = displayName,
                    OldValue = oldValue,
                    NewValue = newValue
                };

                if (record.OldValue is DateTime dateTimeValue && dateTimeValue == default(DateTime))
                {
                    record.OldValue = null;
                }
                if (record.NewValue is DateTime newTimeValue && newTimeValue == default(DateTime))
                {
                    record.NewValue = null;
                }

                return record;
            }
        }

        return list;
    }
    
    
    public override string ToString()
    {
        string data;
        if (TargetRollbackIds is not null)
        {
            data = $"回滚修改{TargetRollbackIds.StringJoin(",")}";
        }
        else
        {
            var sb = new StringBuilder();
            
            foreach (var propertyAlterData in Data.GetChanges())
            {
                sb.Append(Format(propertyAlterData.DisplayName, propertyAlterData.NewValue));
            }

            data = sb.ToString().TrimEnd();
            static string? Format(string name, object? newValue)
            {
                return newValue == null ? null : $"{name}变更为{newValue.ToString()?.BeNullIfEmpty() ?? "[空值]"}\n";
            }
        }

        return IsInvalid ? $"[修改已被{RollbackBy}回滚]\n{data}" : data;
    }

}