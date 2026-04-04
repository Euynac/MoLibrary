using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Monica.Tool.Extensions;

namespace Monica.Framework.Features.AlterChain;


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
    /// Change item ID
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
    /// Refresh DataJson, must be called when Data changes, otherwise it will not take effect
    /// </summary>
    public void RefreshDataJson()
    {
        DataJson = JsonSerializer.Serialize(_data, IMoAlterItem.ALTER_ITEM_SERIALIZER_OPTIONS);
    }

    #endregion

    #region 回滚操作
    /// <summary>
    /// Target rollback ID
    /// </summary>
    public List<string>? TargetRollbackIds { get; set; }

    /// <summary>
    /// Whether it has been rolled back (invalidation flag)
    /// </summary>
    public bool IsInvalid => RollbackBy is not null;

    /// <summary>
    /// (If it has a value, it is considered to have been rolled back) The rollback modification item ID that causes the modification item to be rolled back
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
    /// The change operation time
    /// </summary>
    public DateTime AlterTime { get; set; }

    /// <summary>
    /// The actual change operation impact time, the default is AlterTime
    /// </summary>
    public DateTime? ActualEffectTime { get; set; }

    /// <summary>
    /// The time used when sorting is also the time that is actually applied to the modification.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public DateTime OrderTime => (ActualEffectTime ?? AlterTime).AddMicroseconds(OffsetAdjust);

    /// <summary>
    /// Fine-tune modification time to solve extreme situations such as duplication of change operation time
    /// </summary>
    public int OffsetAdjust { get; set; }

    /// <summary>
    /// Change notes
    /// </summary>
    public string? Remarks { get; set; }
    /// <summary>
    /// Change source
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TEnumAlterSource Source { get; set; } = default!;
    /// <summary>
    /// Change source name
    /// </summary>
    public string? SourceInfo { get; set; }
    /// <summary>
    /// Change source GUID, such as user GUID, etc.
    /// </summary>
    public string? SourceId { get; set; }

    /// <summary>
    /// Change related operation source group ID, which can be used to roll back by affected group
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
    /// Apply changes
    /// </summary>
    public TTargetEntity ApplyAlter(TTargetEntity entity)
    {
        if (IsInvalid || TargetRollbackIds is not null) return entity;
        Data.Apply(entity);
        return entity;
    }
    
    /// <summary>
    /// Get the change description from the original state to apply this change
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
