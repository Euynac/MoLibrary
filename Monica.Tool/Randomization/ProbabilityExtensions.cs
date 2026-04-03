using Monica.Tool.Extensions;

namespace Monica.Tool.Randomization;

/// <summary>
/// Provides probability-based helpers for conditional value selection.
/// </summary>
public static class ProbabilityExtensions
{
    /// <summary>
    /// Returns <see langword="null"/> when the probability test succeeds; otherwise returns the original value.
    /// </summary>
    public static T? MaybeNull<T>(this T value, double probability, double influenceValue = 0,
        double maxProbability = 1, double minProbability = 0)
        where T : class
        => probability.Chance(influenceValue, maxProbability, minProbability) ? null : value;

    /// <summary>
    /// Returns the replacement value when the probability test succeeds; otherwise returns the original value.
    /// </summary>
    public static T MaybeReplace<T>(this T value, T replacement, double probability, double influenceValue = 0,
        double maxProbability = 1, double minProbability = 0)
        => probability.Chance(influenceValue, maxProbability, minProbability) ? replacement : value;

    /// <summary>
    /// Returns <see langword="true"/> when the normalized probability test succeeds.
    /// </summary>
    public static bool Chance(this double probability, double influenceValue = 0, double maxProbability = 1,
        double minProbability = 0)
        => Random.Shared.NextDouble() < NormalizeProbability(probability, influenceValue, maxProbability, minProbability);

    /// <summary>
    /// Returns <see langword="true"/> when the normalized probability test succeeds using a deterministic hash seed.
    /// </summary>
    public static bool Chance(this double probability, object hashSeed, double influenceValue = 0,
        double maxProbability = 1, double minProbability = 0)
        => RandomValueGenerator.NextDouble(0, 1, hashSeed) <
           NormalizeProbability(probability, influenceValue, maxProbability, minProbability);

    private static double NormalizeProbability(double probability, double influenceValue, double maxProbability,
        double minProbability)
        => (probability + influenceValue).LimitInRange(minProbability, maxProbability);
}
