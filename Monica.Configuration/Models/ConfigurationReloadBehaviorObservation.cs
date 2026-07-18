namespace Monica.Configuration.Models;

/// <summary>
/// Represents one service's evidence about how a configuration definition observes changes at runtime.
/// </summary>
public sealed record ConfigurationReloadBehaviorObservation
{
    /// <summary>
    /// Initializes a validated reload-behavior observation.
    /// </summary>
    /// <param name="kind">How the observation was obtained.</param>
    /// <param name="behavior">The observed reload behavior.</param>
    /// <exception cref="ArgumentOutOfRangeException">The kind or behavior is not defined.</exception>
    /// <exception cref="ArgumentException">The behavior is incompatible with the observation kind.</exception>
    public ConfigurationReloadBehaviorObservation(
        ConfigurationReloadBehaviorObservationKind kind,
        ConfigurationReloadBehavior behavior)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported reload-behavior observation kind.");
        }

        if (!Enum.IsDefined(behavior))
        {
            throw new ArgumentOutOfRangeException(nameof(behavior), behavior, "Unsupported configuration reload behavior.");
        }

        var normalizedBehavior = Normalize(behavior);
        if ((kind is ConfigurationReloadBehaviorObservationKind.Declared
                or ConfigurationReloadBehaviorObservationKind.Inferred)
            && normalizedBehavior == ConfigurationReloadBehavior.Unknown)
        {
            throw new ArgumentException(
                $"Observation kind '{kind}' requires a concrete reload behavior.",
                nameof(behavior));
        }

        if ((kind is ConfigurationReloadBehaviorObservationKind.Unresolved
                or ConfigurationReloadBehaviorObservationKind.NotConsumed)
            && normalizedBehavior != ConfigurationReloadBehavior.Unknown)
        {
            throw new ArgumentException(
                $"Observation kind '{kind}' must use '{ConfigurationReloadBehavior.Unknown}'.",
                nameof(behavior));
        }

        Kind = kind;
        Behavior = normalizedBehavior;
    }

    /// <summary>
    /// Gets how the observation was obtained.
    /// </summary>
    public ConfigurationReloadBehaviorObservationKind Kind { get; }

    /// <summary>
    /// Gets the normalized observed behavior. Definition-level <see cref="ConfigurationReloadBehavior.Inherit"/>
    /// is normalized to <see cref="ConfigurationReloadBehavior.Unknown"/>.
    /// </summary>
    public ConfigurationReloadBehavior Behavior { get; }

    /// <summary>
    /// Gets whether the observation constrains the cross-service aggregate.
    /// </summary>
    public bool Participates => Kind != ConfigurationReloadBehaviorObservationKind.NotConsumed;

    /// <summary>
    /// Creates publication evidence from a runtime definition.
    /// </summary>
    /// <param name="definition">The definition being published by the current service.</param>
    /// <returns>A normalized observation with explicit participation semantics.</returns>
    public static ConfigurationReloadBehaviorObservation FromDefinition(ConfigurationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var behavior = Normalize(definition.ReloadBehavior);
        return definition.ReloadBehaviorObservationKind switch
        {
            ConfigurationReloadBehaviorObservationKind.NotConsumed => NotConsumed(),
            ConfigurationReloadBehaviorObservationKind.Unresolved when IsConcrete(behavior) =>
                Declared(behavior),
            ConfigurationReloadBehaviorObservationKind.Declared when IsConcrete(behavior) =>
                Declared(behavior),
            ConfigurationReloadBehaviorObservationKind.Inferred when IsConcrete(behavior) =>
                Inferred(behavior),
            _ => Unresolved()
        };
    }

    /// <summary>
    /// Creates a concrete developer-declared observation.
    /// </summary>
    public static ConfigurationReloadBehaviorObservation Declared(ConfigurationReloadBehavior behavior)
    {
        return new ConfigurationReloadBehaviorObservation(ConfigurationReloadBehaviorObservationKind.Declared, behavior);
    }

    /// <summary>
    /// Creates a concrete service-usage observation.
    /// </summary>
    public static ConfigurationReloadBehaviorObservation Inferred(ConfigurationReloadBehavior behavior)
    {
        return new ConfigurationReloadBehaviorObservation(ConfigurationReloadBehaviorObservationKind.Inferred, behavior);
    }

    /// <summary>
    /// Creates an observation for a possible consumer whose behavior cannot be proven.
    /// </summary>
    public static ConfigurationReloadBehaviorObservation Unresolved()
    {
        return new ConfigurationReloadBehaviorObservation(
            ConfigurationReloadBehaviorObservationKind.Unresolved,
            ConfigurationReloadBehavior.Unknown);
    }

    /// <summary>
    /// Creates a neutral observation for a definition not consumed by the current service.
    /// </summary>
    public static ConfigurationReloadBehaviorObservation NotConsumed()
    {
        return new ConfigurationReloadBehaviorObservation(
            ConfigurationReloadBehaviorObservationKind.NotConsumed,
            ConfigurationReloadBehavior.Unknown);
    }

    /// <summary>
    /// Conservatively combines service observations into the behavior exposed by published metadata.
    /// </summary>
    /// <remarks>
    /// Non-consuming services are neutral. Among participating services, fixed-after-startup and restart-required
    /// evidence outrank uncertainty, while genuine uncertainty outranks online reloadability. This prevents one
    /// known-online consumer from hiding another unresolved consumer.
    /// </remarks>
    /// <param name="observations">Current observations from logical publishing services.</param>
    /// <returns>The deterministic cross-service behavior, or <see cref="ConfigurationReloadBehavior.Unknown"/> when no service participates.</returns>
    public static ConfigurationReloadBehavior Aggregate(
        IEnumerable<ConfigurationReloadBehaviorObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var effective = ConfigurationReloadBehavior.Unknown;
        var highestPriority = 0;
        foreach (var observation in observations)
        {
            ArgumentNullException.ThrowIfNull(observation);
            if (!observation.Participates)
            {
                continue;
            }

            var priority = Priority(observation.Behavior);
            if (priority > highestPriority)
            {
                effective = observation.Behavior;
                highestPriority = priority;
            }
        }

        return effective;
    }

    private static ConfigurationReloadBehavior Normalize(ConfigurationReloadBehavior behavior)
    {
        return behavior == ConfigurationReloadBehavior.Inherit
            ? ConfigurationReloadBehavior.Unknown
            : behavior;
    }

    private static bool IsConcrete(ConfigurationReloadBehavior behavior)
    {
        return behavior is ConfigurationReloadBehavior.OnlineReloadable
            or ConfigurationReloadBehavior.RequiresRestart
            or ConfigurationReloadBehavior.StaticAfterStartup;
    }

    private static int Priority(ConfigurationReloadBehavior behavior)
    {
        return Normalize(behavior) switch
        {
            ConfigurationReloadBehavior.OnlineReloadable => 1,
            ConfigurationReloadBehavior.Unknown => 2,
            ConfigurationReloadBehavior.RequiresRestart => 3,
            ConfigurationReloadBehavior.StaticAfterStartup => 4,
            _ => 0
        };
    }
}
