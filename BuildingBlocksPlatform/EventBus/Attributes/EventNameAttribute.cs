
namespace BuildingBlocksPlatform.EventBus.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class EventNameAttribute(string name) : Attribute, IEventNameProvider
{
    public virtual string Name { get; } = name;

    public static string GetNameOrDefault<TEvent>()
    {
        return GetNameOrDefault(typeof(TEvent));
    }

    public static string GetNameOrDefault(Type eventType)
    {
        return (eventType
                    .GetCustomAttributes(true)
                    .OfType<IEventNameProvider>()
                    .FirstOrDefault()
                    ?.GetName(eventType)
                ?? eventType.FullName)!;
    }

    public string GetName(Type eventType)
    {
        return Name;
    }
}
