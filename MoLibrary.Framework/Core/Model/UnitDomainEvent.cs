using MoLibrary.EventBus.Events;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 领域事件
/// </summary>
/// <param name="type"></param>
public class UnitDomainEvent(Type type) : ProjectUnit(type, EProjectUnitType.DomainEvent), IHasProjectUnitFactory
{
    static UnitDomainEvent()
    {
        AddFactory(Factory);
    }
    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsImplementInterface<IOurDomainEvent>();
    }

    protected override UnitNameConventionOption? DefaultConventionOption()
    {
        return new UnitNameConventionOption
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
    /// 获取领域事件内容结构（new一个事件object默认值）
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
            if (propertyInfo.PropertyType.IsClassObject())
            {
                propertyInfo.SetValue(newObj, GetDefaultPocoObject(propertyInfo.PropertyType));
            }
        }

        return newObj;
    }
}
