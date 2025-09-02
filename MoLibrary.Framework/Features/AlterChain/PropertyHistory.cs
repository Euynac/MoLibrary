
namespace MoLibrary.Framework.Features.AlterChain;

/// <summary>
/// 变更记录属性历史
/// </summary>
/// <typeparam name="TAlterItem"></typeparam>
/// <param name="BeforeValue"></param>
/// <param name="CurrentValue"></param>
/// <param name="RelatedAlterItem"></param>
public record PropertyHistory<TAlterItem>(object? BeforeValue, object? CurrentValue, TAlterItem? RelatedAlterItem)
    where TAlterItem : IMoAlterItem
{
    /// <summary>
    /// 是初始值
    /// </summary>
    public bool IsInitialValue => RelatedAlterItem == null;

}