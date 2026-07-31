using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Monica.Framework.ChangeTracking.Abstractions;
using Monica.Tool.Extensions;

namespace Monica.Framework.ChangeTracking.Models;


public class ChangeChain<TTargetEntity, TAlterItem, TChangeItemData, TEnumAlterSource> where TAlterItem : ChangeItem<TTargetEntity, TChangeItemData, TEnumAlterSource>, new()
    where TChangeItemData : class, IChangeItemData<TTargetEntity>, new() where TEnumAlterSource : Enum
    where TTargetEntity : class, IChangeTrackedEntity
{
    public class ChainJsonParseBridge
    {
        public required string TracingData { get; set; }
        public required List<TAlterItem> ChangingList { get; set; } = [];
    }

    public ChangeChain(ChainJsonParseBridge bridge) :this(bridge.TracingData, bridge.ChangingList)
    {
        
    }

    public ChangeChain(string tracingData, List<TAlterItem> changingList)
    {
        TracingData = tracingData;
        InitChangingChain(changingList);
    }

    public ChangeChain(TTargetEntity entity) : this(entity.GetCurTracingData(), [])
    {
        
    }

    /// <summary>
    /// Last trackable modification time
    /// </summary>
    [JsonIgnore]
    public DateTime? LastModifiedTime { get; protected set; }
    /// <summary>
    /// Original tracking data, starting tracking from this state
    /// </summary>
    [JsonInclude]
    public string TracingData { get; init; }

    /// <summary>
    /// change chain
    /// </summary>
    private readonly SortedList<DateTime, TAlterItem> _changingList = new SortedList<DateTime, TAlterItem>(new DuplicateKeyComparer<DateTime>());

    /// <summary>
    /// change chain
    /// </summary>
    public IReadOnlyList<TAlterItem> ChangingList => GetChangingListSnapshot();

    /// <summary>
    /// Determines whether the change chain already contains an item with the specified ID.
    /// </summary>
    /// <param name="id">Change item ID.</param>
    /// <returns><see langword="true"/> when the ID exists; otherwise <see langword="false"/>.</returns>
    public bool ContainsChangingItemId(string id)
    {
        for (var index = 0; index < _changingList.Count; index++)
        {
            if (string.Equals(_changingList.Values[index].Id, id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates one ordered snapshot of the current change items.
    /// </summary>
    /// <returns>A mutable list that is independent of the chain's internal collection.</returns>
    public List<TAlterItem> GetChangingListSnapshot()
    {
        return _changingList.Values.ToList();
    }
    /// <summary>
    /// Get the change history of the specified change attribute (returned in the order of initial -> last value) (rolled-back AlterItem has been eliminated)
    /// </summary>
    /// <returns></returns>
    public IEnumerable<PropertyHistory<TAlterItem>> GetSingleAlterItemHistory<T1>(Expression<Func<TChangeItemData, T1>> expr)
    {
        return GetSingleAlterItemHistory<T1, bool>(expr, null);
    }
    /// <summary>
    /// Get the change history of the specified change attribute (returned in the order of initial -> last value) (rolled-back AlterItem has been eliminated)
    /// </summary>
    /// <returns></returns>
    public IEnumerable<PropertyHistory<TAlterItem>> GetSingleAlterItemHistory<T1, T2>(Expression<Func<TChangeItemData, T1>> expr,  Expression<Func<TTargetEntity, T2>>? nestedSelectExpr)
    {
        var status = GetInitialStatus();
        var initialValue = GetNestedValue(status)?.GetPropertyValue(expr.GetPropertyInfo().Name);
        var currentValue = initialValue;
        yield return new PropertyHistory<TAlterItem>(null, initialValue, null);

        foreach (var item in ChangingList.Where(p => !p.IsInvalid && expr.GetPropertyValue(p.Data) != null))
        {
            var afterValue = GetNestedValue(item.ApplyAlter(status))?.GetPropertyValue(expr.GetPropertyInfo().Name);
            yield return new PropertyHistory<TAlterItem>(currentValue, afterValue, item);
            currentValue = afterValue;
        }

        object? GetNestedValue(TTargetEntity value)
        {
            if (nestedSelectExpr == null) return value;
            return value.GetPropertyValue(nestedSelectExpr.GetPropertyInfo().Name);
        }
    }
    /// <summary>
    /// Initialize changes
    /// </summary>
    public void InitChangingChain(List<TAlterItem> items)
    {
        _changingList.AddRange(
            items.Select(p =>
                new KeyValuePair<DateTime, TAlterItem>(p.OrderTime, p)), true);
        LastModifiedTime = _changingList.LastOrDefault().Value?.OrderTime;
    }


    /// <summary>
    /// Add changes
    /// </summary>
    /// <param name="item"></param>
    public void Add(TAlterItem item)
    {
        while (_changingList.ContainsKey(item.OrderTime.RoundToSecond()))
        {
            item.OffsetAdjust++;
        }

        _changingList.Add(item.OrderTime, item);


        // Apply rollback items
        if (item.TargetRollbackIds is null)
        {
            LastModifiedTime = _changingList.LastOrDefault().Value?.OrderTime;
            return;
        }
        var dict = new Dictionary<string, TAlterItem>();
        foreach (var alterItem in _changingList)
        {
            alterItem.Value.CancelRollback();
            dict.Add(alterItem.Value.Id, alterItem.Value);
        }

        foreach (var rollbackItem in _changingList.Where(p => p.Value.TargetRollbackIds is not null)
                     .OrderByDescending(p => p.Key))
        {
            if (rollbackItem.Value.IsInvalid) continue;
            foreach (var id in rollbackItem.Value.TargetRollbackIds!)
            {
                if (!dict.TryGetValue(id, out var itemToRollback)) continue;
                itemToRollback.DoRollback(rollbackItem.Value.Id);
            }
        }

        LastModifiedTime = _changingList.LastOrDefault().Value?.OrderTime;
    }

    /// <summary>
    /// Get the initial state of the change chain
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public TTargetEntity GetInitialStatus()
    {
        try
        {
            var status = JsonSerializer.Deserialize<TTargetEntity>(TracingData, ChangeTrackingJsonPolicy.TracingDataOptions);
            if (status == null) throw new Exception();
            return status;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"无法反序列化变更链初始状态，原始追踪数据：{TracingData}", e);
        }
    }


    /// <summary>
    /// Calculate the final state
    /// </summary>
    /// <returns></returns>
    public TTargetEntity GetFinalStatus()
    {
        var origin = GetInitialStatus();
        foreach (var item in ChangingList)
        {
            item.ApplyAlter(origin);
        }

        return origin;
    }

    /// <summary>
    /// Determine whether changes need to be rearranged
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool ShouldReconstruct(TAlterItem item)
    {
        return (LastModifiedTime != null && LastModifiedTime > item.OrderTime) || item.TargetRollbackIds != null;
    }

    /// <summary>
    /// Determine whether they are consistent and generate a change chain patch if they are inconsistent.
    /// </summary>
    public bool IsConsistent(TTargetEntity entity, [NotNullWhen(false)] out TChangeItemData? data)
    {
        data = null;
        if (!entity.Equals(GetFinalStatus()))
        {
            data = new TChangeItemData();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Complete change chain
    /// </summary>
    public void PatchChain()
    {

    }



    /// <summary>
    /// Create an operation to roll back all changes with the specified source ID. If no relevant items that need to be rolled back are found, null will be returned.
    /// </summary>
    public TAlterItem? CreateRollbackAlterItemBySourceId(string id, TEnumAlterSource source, string sourceId, string? sourceInfo, string targetSourceId)
    {
        var targetIds = new List<string>();
        var rollbackTime = DateTime.Now;
        ChangingList.Where(p => (p.SourceId == targetSourceId || p.RelatedSourceId?.Contains(targetSourceId) is true)  && p.AlterTime < rollbackTime)
            .Do(p => targetIds.Add(p.Id));

        if (targetIds.Count == 0) return null;
        var rollbackItem = new TAlterItem()
        {
            Id = id,
            Source = source,
            SourceId = sourceId,
            SourceInfo = sourceInfo,
            AlterTime = rollbackTime,
            TargetRollbackIds = targetIds,
            Data = new TChangeItemData()
        };
        return rollbackItem;
    }

}
