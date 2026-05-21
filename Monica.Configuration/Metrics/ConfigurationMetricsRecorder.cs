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

    private readonly Counter<long> _mutationFailureCounter = meterFactory
        .Create(ConfigurationMetrics.MeterName)
        .CreateCounter<long>(ConfigurationMetrics.MutationFailureCount);

    private readonly Histogram<double> _reloadLatencyHistogram = meterFactory
        .Create(ConfigurationMetrics.MeterName)
        .CreateHistogram<double>(ConfigurationMetrics.ReloadLatency, "ms");

    /// <summary>
    /// Records one mutation.
    /// </summary>
    /// <param name="storeKey">The store key.</param>
    public void RecordMutation(string storeKey)
    {
        _mutationCounter.Add(1, new KeyValuePair<string, object?>("store", storeKey));
    }

    /// <summary>
    /// Records one failed mutation.
    /// </summary>
    /// <param name="storeKey">The store key.</param>
    /// <param name="exceptionType">The exception type.</param>
    public void RecordMutationFailure(string storeKey, string exceptionType)
    {
        _mutationFailureCounter.Add(
            1,
            new KeyValuePair<string, object?>("store", storeKey),
            new KeyValuePair<string, object?>("exception.type", exceptionType));
    }

    /// <summary>
    /// Records one provider reload duration.
    /// </summary>
    /// <param name="duration">The reload duration.</param>
    public void RecordReloadLatency(TimeSpan duration)
    {
        _reloadLatencyHistogram.Record(duration.TotalMilliseconds);
    }
}
