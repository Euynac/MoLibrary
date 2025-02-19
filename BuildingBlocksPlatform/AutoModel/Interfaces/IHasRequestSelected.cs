namespace BuildingBlocksPlatform.AutoModel.Interfaces;

public interface IHasRequestSelect
{
    /// <summary>
    /// 选择部分字段返回条件
    /// </summary>
    string? SelectColumns { get; set; }

    /// <summary>
    /// 选择除了给定字段的返回条件(不可与<see cref="SelectColumns"/>同时使用)
    /// </summary>
    string? SelectExceptColumns { get; set; }

    /// <summary>
    /// 是否进行了选择操作
    /// </summary>

    bool HasUsingSelected()
    {
        return !string.IsNullOrWhiteSpace(SelectColumns) || !string.IsNullOrWhiteSpace(SelectExceptColumns);
    }
}