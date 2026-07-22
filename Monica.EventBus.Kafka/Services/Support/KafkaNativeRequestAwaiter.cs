namespace Monica.EventBus.Kafka.Services.Support;

/// <summary>
/// Awaits Confluent.Kafka requests without abandoning their native task on cancellation.
/// </summary>
/// <remarks>
/// Confluent admin methods expose tasks but do not accept a cancellation token. Using
/// <see cref="Task.WaitAsync(CancellationToken)"/> would cancel only the managed wait and allow the
/// caller to dispose the native client while librdkafka is still completing the request. The
/// resulting use-after-free can terminate the process. These helpers let the request finish (the
/// Kafka request timeout remains the upper bound) and then propagate cancellation to the caller.
/// </remarks>
internal static class KafkaNativeRequestAwaiter
{
    /// <summary>
    /// Awaits a native Kafka request and observes cancellation after the request has completed.
    /// </summary>
    public static async Task<T> AwaitAsync<T>(Task<T> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await request.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>
    /// Awaits a non-generic native Kafka request and observes cancellation after completion.
    /// </summary>
    public static async Task AwaitAsync(Task request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await request.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
