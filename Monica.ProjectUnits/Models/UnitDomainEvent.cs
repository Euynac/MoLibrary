using Monica.EventBus.Events;
using Monica.ProjectUnits.Services.Support;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Describes a discovered domain-event payload.
/// </summary>
public class UnitDomainEvent : ProjectUnit
{
    internal UnitDomainEvent(Type type, ProjectUnitCatalog catalog)
        : base(type, EProjectUnitType.DomainEvent, catalog)
    {
    }

    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsImplementInterface<IDomainEvent>() && typeof(DomainEvent) != Type;
    }

    protected override ProjectUnitNamingRule? DefaultConventionOption()
    {
        return new ProjectUnitNamingRule
        {
            Prefix = "Event"
        };
    }

    internal static ProjectUnit? Create(Type type, ProjectUnitCatalog catalog)
    {
        var unit = new UnitDomainEvent(type, catalog);
        return unit.VerifyType() ? unit : null;
    }

    /// <summary>
    /// Creates a representative event payload populated with recursively constructed default reference values.
    /// </summary>
    /// <returns>A representative payload instance, or <see langword="null"/> when the event cannot be instantiated.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a writable reference-type property cannot be populated with its representative default value.
    /// </exception>
    public object? GetStructure()
    {
        return GetDefaultPocoObject(Type);
    }

    private static object? GetDefaultPocoObject(Type pocoType)
    {
        var newObj = Activator.CreateInstance(pocoType);
        if (pocoType.IsImplementInterfaceGeneric(typeof(IEnumerable<>))) return newObj;
        var properties = pocoType.GetProperties();
        foreach (var propertyInfo in properties)
        {
            if (propertyInfo.PropertyType.IsClassObject() && propertyInfo.CanWrite)
            {
                try
                {
                    propertyInfo.SetValue(newObj, GetDefaultPocoObject(propertyInfo.PropertyType));
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Failed to assign a default value to property '{propertyInfo.Name}' on " +
                        $"{pocoType.GetCleanFullName()}. The property type is " +
                        $"{propertyInfo.PropertyType.GetCleanFullName()}.",
                        exception);
                }
            }
        }

        return newObj;
    }
}
