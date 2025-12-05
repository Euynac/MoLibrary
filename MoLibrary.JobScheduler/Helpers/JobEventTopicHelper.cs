using MoLibrary.EventBus.Attributes;

namespace MoLibrary.JobScheduler.Helpers;

/// <summary>
/// Helper for generating EventBus topic names with project isolation.
/// </summary>
public static class JobEventTopicHelper
{
    /// <summary>
    /// Generates topic name for job events with project isolation.
    /// Format: {FromProject}.{EventName}
    /// </summary>
    /// <typeparam name="TEvent">Event type</typeparam>
    /// <param name="fromProject">Source project identifier</param>
    /// <returns>Formatted topic name</returns>
    /// <exception cref="ArgumentException">When fromProject is null or empty</exception>
    public static string GetTopicName<TEvent>(string fromProject) where TEvent : class
    {
        return GetTopicName(typeof(TEvent), fromProject);
    }

    /// <summary>
    /// Generates topic name for job events with project isolation.
    /// Format: {FromProject}.{EventName}
    /// </summary>
    /// <param name="eventType">Event type</param>
    /// <param name="fromProject">Source project identifier</param>
    /// <returns>Formatted topic name</returns>
    /// <exception cref="ArgumentException">When fromProject is null or empty</exception>
    public static string GetTopicName(Type eventType, string fromProject)
    {
        if (string.IsNullOrWhiteSpace(fromProject))
            throw new ArgumentException("FromProject cannot be null or empty", nameof(fromProject));

        var eventName = EventNameAttribute.GetNameOrDefault(eventType);
        return $"{fromProject}.{eventName}";
    }
}
