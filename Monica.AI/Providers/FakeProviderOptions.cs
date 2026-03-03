namespace Monica.AI.Providers;

/// <summary>
/// Fake provider options for local embedding testing.
/// </summary>
public class FakeProviderOptions : AIProviderOptions
{
    /// <summary>
    /// Default embedding dimensions when model metadata has no explicit dimensions.
    /// </summary>
    public int DefaultDimensions { get; set; } = 384;
}
