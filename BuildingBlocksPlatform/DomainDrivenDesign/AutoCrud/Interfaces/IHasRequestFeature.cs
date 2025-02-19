namespace BuildingBlocksPlatform.DomainDrivenDesign.AutoCrud.Interfaces;

public interface IHasRequestFeature
{
    /// <summary>
    /// 额外特性功能表达式。目前支持：Distinct（对Select后结果进行去重）
    /// </summary>
    public string? Features { get; set; }

    public FeatureSetting? GetSetting()
    {
        if (string.IsNullOrEmpty(Features)) return null;
        ERequestFeature features = default;
        foreach (var key in Features.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse(key, true, out ERequestFeature feature))
            {
                features |= feature;
            }
        }
        return features == default ? null : new FeatureSetting { FeatureFlags = features };
    }
}

public class FeatureSetting
{
    /// <summary>
    /// 特性枚举
    /// </summary>
    public ERequestFeature? FeatureFlags { get; set; }
    /// <summary>
    /// 特性是否使得Count失效，需要另外Count最终结果
    /// </summary>
    /// <returns></returns>
    public bool ShouldJumpCount()
    {
        return FeatureFlags?.HasFlag(ERequestFeature.Distinct) is true;
    }
}
[Flags]
public enum ERequestFeature
{
    /// <summary>
    /// 结果去重，仅支持Select后进行去重
    /// </summary>
    Distinct = 1 << 0,
}