using System.Diagnostics.Metrics;

namespace Monica.Configuration.Metrics;

/// <summary>
/// Records Monica.Configuration metrics.
/// </summary>
public sealed class ConfigurationMetricsRecorder(IMeterFactory meterFactory)
{
    private readonly Counter<long> _mutationCounter = meterFactory
        .Create(ConfigurationMetrics.MeterName)
        .CreateCounter<long>(ConfigurationMetrics.MutationCount);

    /// <summary>
    /// Records one mutation.
    /// </summary>
    /// <param name="sourceKey">The source key.</param>
    public void RecordMutation(string sourceKey)
    {
        _mutationCounter.Add(1, new KeyValuePair<string, object?>("source", sourceKey));
    }
}
