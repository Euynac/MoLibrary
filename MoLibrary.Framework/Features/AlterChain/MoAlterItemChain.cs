using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Features.AlterChain;


public class MoAlterItemChain<TTargetEntity, TAlterItem, TAlterItemData, TEnumAlterSource> where TAlterItem : MoAlterItem<TTargetEntity, TAlterItemData, TEnumAlterSource>, new()
    where TAlterItemData : class, IMoTracingDataAlterItemData<TTargetEntity>, new() where TEnumAlterSource : Enum
    where TTargetEntity : class, IMoTracingDataEntity
{
    public class ChainJsonParseBridge
    {
        public required string TracingData { get; set; }
        public required List<TAlterItem> ChangingList { get; set; } = [];
    }

    public MoAlterItemChain(ChainJsonParseBridge bridge) :this(bridge.TracingData, bridge.ChangingList)
    {
        
    }

    public MoAlterItemChain(string tracingData, List<TAlterItem> changingList)
    {
        TracingData = tracingData;
        InitChangingChain(changingList);
    }

    public MoAlterItemChain(TTargetEntity entity) : this(entity.GetCurTracingData(), [])
    {
        
    }

    /// <summary>
    /// 最后一次可跟踪的修改时间
    /// </summary>
    [JsonIgnore]
    public DateTime? LastModifiedTime { get; protected set; }
    /// <summary>
    /// 原始追踪数据，从该状态开始追踪
    /// </summary>
    [JsonInclude]
    public string TracingData { get; init; }

    /// <summary>
    /// 变更链
    /// </summary>
    private readonly SortedList<DateTime, TAlterItem> _changingList = new SortedList<DateTime, TAlterItem>(new DuplicateKeyComparer<DateTime>());

    /// <summary>
    /// 变更链
    /// </summary>
    public IReadOnlyList<TAlterItem> ChangingList => _changingList.Values.ToList();
    /// <summary>
    /// 获取指定变更属性变更历史（按照最初->最后的值顺序返回）（已剔除回滚过的AlterItem）
    /// </summary>
    /// <returns></returns>
    public IEnumerable<PropertyHistory<TAlterItem>> GetSingleAlterItemHistory<T1>(Expression<Func<TAlterItemData, T1>> expr)
    {
        return GetSingleAlterItemHistory<T1, bool>(expr, null);
    }
    /// <summary>
    /// 获取指定变更属性变更历史（按照最初->最后的值顺序返回）（已剔除回滚过的AlterItem）
    /// </summary>
    /// <returns></returns>
    public IEnumerable<PropertyHistory<TAlterItem>> GetSingleAlterItemHistory<T1, T2>(Expression<Func<TAlterItemData, T1>> expr,  Expression<Func<TTargetEntity, T2>>? nestedSelectExpr)
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
    /// 初始化变更
    /// </summary>
    public void InitChangingChain(List<TAlterItem> items)
    {
        _changingList.AddRange(
            items.Select(p =>
                new KeyValuePair<DateTime, TAlterItem>(p.OrderTime, p)), true);
        LastModifiedTime = _changingList.LastOrDefault().Value?.OrderTime;
    }


    /// <summary>
    /// 添加变更
    /// </summary>
    /// <param name="item"></param>
    public void Add(TAlterItem item)
    {
        while (_changingList.ContainsKey(item.OrderTime.RoundToSecond()))
        {
            item.OffsetAdjust++;
        }

        _changingList.Add(item.OrderTime, item);


        // 应用回滚项
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
    /// 获取变更链初始状态
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public TTargetEntity GetInitialStatus()
    {
        try
        {
            var status = JsonSerializer.Deserialize<TTargetEntity>(TracingData, IMoTracingDataEntity.TRACING_DATA_JSON_OPTIONS);
            if (status == null) throw new Exception();
            return status;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"无法反序列化变更链初始状态，原始追踪数据：{TracingData}", e);
        }
    }


    /// <summary>
    /// 计算最终状态
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
    /// 判断是否需要重排变更项
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool ShouldReconstruct(TAlterItem item)
    {
        return (LastModifiedTime != null && LastModifiedTime > item.OrderTime) || item.TargetRollbackIds != null;
    }

    /// <summary>
    /// 判断是否一致，若不一致生成变更链补丁
    /// </summary>
    public bool IsConsistent(TTargetEntity entity, [NotNullWhen(false)] out TAlterItemData? data)
    {
        data = null;
        if (!entity.Equals(GetFinalStatus()))
        {
            data = new TAlterItemData();
            return false;
        }

        return true;
    }

    /// <summary>
    /// 补全变更链
    /// </summary>
    public void PatchChain()
    {

    }



    /// <summary>
    /// 创建将指定来源ID的所有变更项回滚操作，如果没有找到相关需要回滚项，返回null
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
            Data = new TAlterItemData()
        };
        return rollbackItem;
    }

}