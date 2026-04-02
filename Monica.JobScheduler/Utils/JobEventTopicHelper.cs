using Monica.EventBus.Annotations;

namespace Monica.JobScheduler.Utils;

/// <summary>
/// Helper for generating EventBus topic names with project isolation.
/// </summary>
public static class JobEventTopicHelper
{
    /// <summary>
    /// Generates topic name for scope-isolated job events.
    /// Format: {SchedulerScopeKey}.{EventName}
    /// </summary>
    /// <typeparam name="TEvent">Event type</typeparam>
    /// <param name="schedulerScopeKey">Scheduler scope identifier</param>
    /// <returns>Formatted topic name</returns>
    /// <exception cref="ArgumentException">When schedulerScopeKey is null or empty</exception>
    public static string GetTopicName<TEvent>(string schedulerScopeKey) where TEvent : class
    {
        return GetTopicName(typeof(TEvent), schedulerScopeKey);
    }

    /// <summary>
    /// Generates topic name for scope-isolated job events.
    /// Format: {SchedulerScopeKey}.{EventName}
    /// </summary>
    /// <param name="eventType">Event type</param>
    /// <param name="schedulerScopeKey">Scheduler scope identifier</param>
    /// <returns>Formatted topic name</returns>
    /// <exception cref="ArgumentException">When schedulerScopeKey is null or empty</exception>
    public static string GetTopicName(Type eventType, string schedulerScopeKey)
    {
        if (string.IsNullOrWhiteSpace(schedulerScopeKey))
            throw new ArgumentException("SchedulerScopeKey cannot be null or empty", nameof(schedulerScopeKey));

        var eventName = EventNameAttribute.GetNameOrDefault(eventType);
        return $"{schedulerScopeKey}.{eventName}";
    }

    /// <summary>
    /// Generates topic name for job execution events with both scope and project isolation.
    /// Format: {SchedulerScopeKey}.{FromProject}.{EventName}
    /// </summary>
    public static string GetProjectTopicName<TEvent>(string schedulerScopeKey, string fromProject) where TEvent : class
    {
        return GetProjectTopicName(typeof(TEvent), schedulerScopeKey, fromProject);
    }

    /// <summary>
    /// Generates topic name for job execution events with both scope and project isolation.
    /// Format: {SchedulerScopeKey}.{FromProject}.{EventName}
    /// </summary>
    public static string GetProjectTopicName(Type eventType, string schedulerScopeKey, string fromProject)
    {
        if (string.IsNullOrWhiteSpace(schedulerScopeKey))
            throw new ArgumentException("SchedulerScopeKey cannot be null or empty", nameof(schedulerScopeKey));

        if (string.IsNullOrWhiteSpace(fromProject))
            throw new ArgumentException("FromProject cannot be null or empty", nameof(fromProject));

        var eventName = EventNameAttribute.GetNameOrDefault(eventType);
        return $"{schedulerScopeKey}.{fromProject}.{eventName}";
    }
}
