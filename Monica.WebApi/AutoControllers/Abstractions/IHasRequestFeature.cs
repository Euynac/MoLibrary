namespace Monica.WebApi.AutoControllers.Abstractions;

public interface IHasRequestFeature
{
    /// <summary>
    /// Additional request features.
    /// Currently supports <c>Distinct</c>, which removes duplicates after <c>Select</c> is applied.
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
    /// Parsed feature flags.
    /// </summary>
    public ERequestFeature? FeatureFlags { get; set; }
    /// <summary>
    /// Determines whether the selected features invalidate the initial count query and require counting the final result instead.
    /// </summary>
    /// <returns><see langword="true"/> when the final result set must be counted; otherwise, <see langword="false"/>.</returns>
    public bool ShouldJumpCount()
    {
        return FeatureFlags?.HasFlag(ERequestFeature.Distinct) is true;
    }
}
[Flags]
public enum ERequestFeature
{
    /// <summary>
    /// Deduplicates the result set. Only supported after <c>Select</c> is applied.
    /// </summary>
    Distinct = 1 << 0,
}
