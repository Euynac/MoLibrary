namespace BuildingBlocksPlatform.AutoModel.Interfaces;

public interface IHasRequestFilter
{
    /// <summary>
    /// 动态过滤条件
    /// </summary>
    string? Filter { get; set; }
    /// <summary>
    /// 模糊查询多个字段的值
    /// </summary>
    string? Fuzzy { get; set; }
    /// <summary>
    /// 设置模糊查询指定字段
    /// </summary>
    string? FuzzyColumns { get; set; }
}