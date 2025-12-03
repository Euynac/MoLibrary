using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Attributes;

namespace MoLibrary.EventBus.Models;

/// <summary>
/// Represents a registered event handler with pre-computed metadata
/// </summary>
public sealed record EventHandlerRegisterInfo
{
    public Type HandlerType { get; }
    public Type EventType { get; }
    public string TopicName { get; }
    public bool IsDistributed { get; }
    public bool IsLocal { get; }
    /// <summary>
    /// 是否是自动注册的
    /// </summary>
    public bool IsAutoRegistered { get; }

    public EventHandlerRegisterInfo(
        Type handlerType,
        Type eventType,
        string topicName,
        bool isDistributed,
        bool isLocal,
        bool isAutoRegistered = false)
    {
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        TopicName = topicName ?? throw new ArgumentNullException(nameof(topicName));

        // Validation: exactly one scope must be true
        if (isDistributed == isLocal)
        {
            throw new InvalidOperationException(
                $"Handler '{handlerType.FullName}' must implement either " +
                $"IMoDistributedEventHandler<{eventType.Name}> OR " +
                $"IMoLocalEventHandler<{eventType.Name}>, but not both for the same event type.");
        }

        IsDistributed = isDistributed;
        IsLocal = isLocal;
        IsAutoRegistered = isAutoRegistered;
    }

    /// <summary>
    /// Factory method to create registrations from handler type with automatic interface detection
    /// </summary>
    public static IEnumerable<EventHandlerRegisterInfo> CreateFromHandlerType(Type handlerType)
    {
        var results = new List<EventHandlerRegisterInfo>();
        var distributedEvents = new HashSet<Type>();
        var localEvents = new HashSet<Type>();

        foreach (var @interface in handlerType.GetInterfaces().Where(p => p.IsGenericType))
        {
            var eventType = @interface.GetGenericArguments()[0];
            if (eventType.IsGenericParameter) continue; // 跳过泛型参数
            if (@interface.GetGenericTypeDefinition() == typeof(IMoDistributedEventHandler<>))
            {
                distributedEvents.Add(eventType);
            }
            else if (@interface.GetGenericTypeDefinition() == typeof(IMoLocalEventHandler<>))
            {
                localEvents.Add(eventType);
            }
        }

        // Validate no overlap
        var overlap = distributedEvents.Intersect(localEvents).ToList();
        if (overlap.Count != 0)
        {
            throw new InvalidOperationException(
                $"Handler '{handlerType.FullName}' implements both IMoDistributedEventHandler and " +
                $"IMoLocalEventHandler for the same event type(s): {string.Join(", ", overlap.Select(t => t.Name))}. " +
                $"A handler cannot process the same event type through both distributed and local buses.");
        }

        // Create registrations for distributed handlers
        foreach (var eventType in distributedEvents)
        {
            var topicName = EventNameAttribute.GetNameOrDefault(eventType);
            results.Add(new EventHandlerRegisterInfo(
                handlerType, eventType, topicName,
                isDistributed: true, isLocal: false, isAutoRegistered: true));
        }

        // Create registrations for local handlers
        foreach (var eventType in localEvents)
        {
            var topicName = EventNameAttribute.GetNameOrDefault(eventType);
            results.Add(new EventHandlerRegisterInfo(
                handlerType, eventType, topicName,
                isDistributed: false, isLocal: true, isAutoRegistered: true));
        }

        return results;
    }
}
