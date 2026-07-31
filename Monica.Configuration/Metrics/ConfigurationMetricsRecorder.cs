using System.Diagnostics.Metrics;

namespace Monica.Configuration.Metrics;

/// <summary>
/// Records Monica.Configuration metrics.
/// </summary>
public sealed class ConfigurationMetricsRecorder(IMeterFactory meterFactory)
{
    private const string RESULT_TAG_NAME = "result";
    private const string STAGE_TAG_NAME = "stage";

    private readonly Counter<long> _mutationCounter = meterFactory
        .Create(ConfigurationMetrics.MeterName)
        .CreateCounter<long>(ConfigurationMetrics.MutationCount);

    private readonly Counter<long> _mutationFailureCounter = meterFactory
        .Create(ConfigurationMetrics.MeterName)
        .CreateCounter<long>(ConfigurationMetrics.MutationFailureCount);

    private readonly Histogram<double> _reloadLatencyHistogram = meterFactory
        .Create(ConfigurationMetrics.MeterName)
        .CreateHistogram<double>(ConfigurationMetrics.ReloadLatency, "ms");

    private readonly Counter<long> _reloadNotificationFailureCounter = meterFactory
        .Create(ConfigurationMetrics.MeterName)
        .CreateCounter<long>(ConfigurationMetrics.ReloadNotificationFailureCount);

    private readonly Histogram<double> _startupStageDurationHistogram = meterFactory
        .Create(ConfigurationMetrics.MeterName)
        .CreateHistogram<double>(
            ConfigurationMetrics.StartupStageDuration,
            "ms",
            "Duration of bounded Monica.Configuration startup stages.");

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

    /// <summary>
    /// Records one failed distributed reload notification.
    /// </summary>
    /// <param name="notifier">Stable notifier type name.</param>
    /// <param name="operation">Stable notification operation.</param>
    /// <param name="exceptionType">Exception type.</param>
    public void RecordNotificationFailure(string notifier, string operation, string exceptionType)
    {
        _reloadNotificationFailureCounter.Add(
            1,
            new KeyValuePair<string, object?>("notifier", notifier),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("exception.type", exceptionType));
    }

    /// <summary>
    /// Measures an asynchronous configuration startup stage.
    /// </summary>
    internal async Task MeasureStartupStageAsync(
        ConfigurationStartupStage stage,
        Func<Task> operation)
    {
        var started = TimeProvider.System.GetTimestamp();
        var result = ConfigurationStartupResult.Success;
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            result = ConfigurationStartupResult.Cancelled;
            throw;
        }
        catch
        {
            result = ConfigurationStartupResult.Failure;
            throw;
        }
        finally
        {
            RecordStartupStageDuration(stage, result, TimeProvider.System.GetElapsedTime(started));
        }
    }

    /// <summary>
    /// Measures a synchronous configuration startup stage.
    /// </summary>
    internal T MeasureStartupStage<T>(ConfigurationStartupStage stage, Func<T> operation)
    {
        var started = TimeProvider.System.GetTimestamp();
        var result = ConfigurationStartupResult.Success;
        try
        {
            return operation();
        }
        catch (OperationCanceledException)
        {
            result = ConfigurationStartupResult.Cancelled;
            throw;
        }
        catch
        {
            result = ConfigurationStartupResult.Failure;
            throw;
        }
        finally
        {
            RecordStartupStageDuration(stage, result, TimeProvider.System.GetElapsedTime(started));
        }
    }

    /// <summary>
    /// Records one startup stage duration with bounded stage and result dimensions.
    /// </summary>
    internal void RecordStartupStageDuration(
        ConfigurationStartupStage stage,
        ConfigurationStartupResult result,
        TimeSpan duration)
    {
        _startupStageDurationHistogram.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>(STAGE_TAG_NAME, GetStageTagValue(stage)),
            new KeyValuePair<string, object?>(RESULT_TAG_NAME, GetResultTagValue(result)));
    }

    private static string GetStageTagValue(ConfigurationStartupStage stage) => stage switch
    {
        ConfigurationStartupStage.MetadataPublication => "metadata-publication",
        ConfigurationStartupStage.ProjectionReload => "projection-reload",
        ConfigurationStartupStage.ProviderActivation => "provider-activation",
        ConfigurationStartupStage.RuntimeValidation => "runtime-validation",
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unsupported configuration startup stage.")
    };

    private static string GetResultTagValue(ConfigurationStartupResult result) => result switch
    {
        ConfigurationStartupResult.Success => "success",
        ConfigurationStartupResult.Failure => "failure",
        ConfigurationStartupResult.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unsupported configuration startup result.")
    };
}

internal enum ConfigurationStartupStage
{
    MetadataPublication,
    ProjectionReload,
    ProviderActivation,
    RuntimeValidation
}

internal enum ConfigurationStartupResult
{
    Success,
    Failure,
    Cancelled
}
