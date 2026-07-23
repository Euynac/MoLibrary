using Monica.Modules;

namespace Monica.Dapr.Services;

/// <summary>
/// Validates and calculates the retry schedule for one streaming-subscription supervisor.
/// </summary>
internal sealed class DaprSubscriptionRecoveryPolicy
{
    private const double MINIMUM_JITTER_FACTOR = 0.8d;

    private DaprSubscriptionRecoveryPolicy(
        TimeSpan initialDelay,
        TimeSpan maximumDelay,
        TimeSpan stabilityPeriod,
        double backoffMultiplier)
    {
        InitialDelay = initialDelay;
        MaximumDelay = maximumDelay;
        StabilityPeriod = stabilityPeriod;
        BackoffMultiplier = backoffMultiplier;
    }

    public TimeSpan InitialDelay { get; }

    public TimeSpan MaximumDelay { get; }

    public TimeSpan StabilityPeriod { get; }

    private double BackoffMultiplier { get; }

    public static DaprSubscriptionRecoveryPolicy Create(ModuleDaprEventBusOption options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ValidateDelay(options.SubscriptionRecoveryInitialDelay, nameof(options.SubscriptionRecoveryInitialDelay));
        ValidateDelay(options.SubscriptionRecoveryMaxDelay, nameof(options.SubscriptionRecoveryMaxDelay));
        ValidateDelay(options.SubscriptionRecoveryStabilityPeriod, nameof(options.SubscriptionRecoveryStabilityPeriod));

        if (options.SubscriptionRecoveryMaxDelay < options.SubscriptionRecoveryInitialDelay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.SubscriptionRecoveryMaxDelay),
                options.SubscriptionRecoveryMaxDelay,
                "The maximum subscription recovery delay cannot be shorter than the initial delay.");
        }

        if (!double.IsFinite(options.SubscriptionRecoveryBackoffMultiplier)
            || options.SubscriptionRecoveryBackoffMultiplier < 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.SubscriptionRecoveryBackoffMultiplier),
                options.SubscriptionRecoveryBackoffMultiplier,
                "The subscription recovery backoff multiplier must be finite and at least 1.");
        }

        return new DaprSubscriptionRecoveryPolicy(
            options.SubscriptionRecoveryInitialDelay,
            options.SubscriptionRecoveryMaxDelay,
            options.SubscriptionRecoveryStabilityPeriod,
            options.SubscriptionRecoveryBackoffMultiplier);
    }

    public TimeSpan CalculateDelay(int consecutiveFailures, string topicName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(consecutiveFailures);

        var multiplier = Math.Pow(BackoffMultiplier, consecutiveFailures - 1);
        var uncappedMilliseconds = InitialDelay.TotalMilliseconds * multiplier;
        var cappedMilliseconds = double.IsFinite(uncappedMilliseconds)
            ? Math.Min(uncappedMilliseconds, MaximumDelay.TotalMilliseconds)
            : MaximumDelay.TotalMilliseconds;

        // A stable per-topic factor spreads recovery attempts without making tests or diagnostics nondeterministic.
        var topicHash = unchecked((uint)StringComparer.Ordinal.GetHashCode(topicName));
        var normalizedHash = topicHash / (double)uint.MaxValue;
        var jitterFactor = MINIMUM_JITTER_FACTOR + ((1d - MINIMUM_JITTER_FACTOR) * normalizedHash);
        return TimeSpan.FromMilliseconds(cappedMilliseconds * jitterFactor);
    }

    private static void ValidateDelay(TimeSpan delay, string parameterName)
    {
        if (delay <= TimeSpan.Zero || delay.TotalMilliseconds > uint.MaxValue - 1d)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                delay,
                $"The delay must be greater than zero and no longer than {uint.MaxValue - 1} milliseconds.");
        }
    }
}
