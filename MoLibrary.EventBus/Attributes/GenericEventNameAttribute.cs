using MoLibrary.Tool.Extensions;

namespace MoLibrary.EventBus.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class GenericEventNameAttribute : Attribute, IEventNameProvider
{
    public string? Prefix { get; set; }

    public string? Postfix { get; set; }

    public virtual string GetName(Type eventType)
    {
        if (!eventType.IsGenericType)
        {
            throw new Exception($"Given type is not generic: {eventType.AssemblyQualifiedName}");
        }

        var genericArguments = eventType.GetGenericArguments();
        if (genericArguments.Length > 1)
        {
            throw new Exception($"Given type has more than one generic argument: {eventType.AssemblyQualifiedName}");
        }

        var eventName = EventNameAttribute.GetNameOrDefault(genericArguments[0]);

        if (!Prefix.IsNullOrEmpty())
        {
            eventName = Prefix + eventName;
        }

        if (!Postfix.IsNullOrEmpty())
        {
            eventName += Postfix;
        }

        return eventName;
    }
}
