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

    public EventHandlerRegisterInfo(
        Type handlerType,
        Type eventType,
        string topicName,
        bool isDistributed,
        bool isLocal)
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
    }

    /// <summary>
    /// Factory method to create registrations from handler type with automatic interface detection
    /// </summary>
    public static IEnumerable<EventHandlerRegisterInfo> CreateFromHandlerType(Type handlerType)
    {
        var results = new List<EventHandlerRegisterInfo>();
       
        foreach (var @interface in handlerType.GetInterfaces().Where(p=>p.IsGenericType))
        {
            var eventType = @interface.GetGenericArguments()[0];
            var topicName = EventNameAttribute.GetNameOrDefault(eventType);
            var isDistributed = @interface.GetGenericTypeDefinition() == typeof(IMoDistributedEventHandler<>);
            var isLocal = @interface.GetGenericTypeDefinition() == typeof(IMoLocalEventHandler<>);
           
            results.Add(new EventHandlerRegisterInfo(
                handlerType, eventType, topicName,
                isDistributed, isLocal));
        }
        
    

        return results;
    }
}
