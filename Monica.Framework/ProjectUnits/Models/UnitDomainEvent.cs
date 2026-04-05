using Monica.EventBus.Events;
using Monica.Framework.ProjectUnits.Abstractions;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.Framework.ProjectUnits.Models;

/// <summary>
/// domain events
/// </summary>
/// <param name="type"></param>
public class UnitDomainEvent(Type type) : ProjectUnit(type, EProjectUnitType.DomainEvent), IHasProjectUnitFactory
{
    static UnitDomainEvent()
    {
        AddUnitRegisterFactory(Factory);
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

    public static ProjectUnit? Factory(FactoryContext context)
    {
        var unit = new UnitDomainEvent(context.Type);
        return unit.VerifyType() ? unit : null;
    }
   
    /// <summary>
    /// Get the domain event content structure (new event object default value)
    /// </summary>
    /// <returns></returns>
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
                catch (Exception e)
                {
                    throw new Exception($"{pocoType.GetCleanFullName()}设置属性{propertyInfo.PropertyType.GetCleanFullName()}默认值出错", e);
                }
            }
        }

        return newObj;
    }
}
