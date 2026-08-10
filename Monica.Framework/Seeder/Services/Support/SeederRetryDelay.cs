namespace Monica.Framework.Seeder.Services.Support;

internal sealed class SeederRetryDelay(TimeProvider timeProvider) : ISeederRetryDelay
{
    private const double JITTER_RATIO = 0.2;

    public Task DelayAsync(
        int failedAttempt,
        Monica.Modules.ModuleSeederOption options,
        CancellationToken cancellationToken)
    {
        var exponent = Math.Min(failedAttempt - 1, 30);
        var exponentialMilliseconds = options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, exponent);
        var cappedMilliseconds = Math.Min(exponentialMilliseconds, options.RetryMaxDelay.TotalMilliseconds);
        var jitterMultiplier = 1 + ((Random.Shared.NextDouble() * 2 - 1) * JITTER_RATIO);
        var jitteredMilliseconds = Math.Min(
            options.RetryMaxDelay.TotalMilliseconds,
            cappedMilliseconds * jitterMultiplier);
        var delay = TimeSpan.FromMilliseconds(Math.Max(1, jitteredMilliseconds));
        return Task.Delay(delay, timeProvider, cancellationToken);
    }
}
