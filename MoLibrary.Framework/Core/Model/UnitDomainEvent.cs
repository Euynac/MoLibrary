using MoLibrary.EventBus.Events;
using MoLibrary.Framework.Core.Interfaces;
using MoLibrary.Framework.Modules;
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
        AddUnitRegisterFactory(Factory);
    }
    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsImplementInterface<IMoDomainEvent>() && typeof(MoDomainEvent) != Type;
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
