using Monica.DomainDrivenDesign.Interfaces;
using Monica.Framework.Core.Interfaces;
using Monica.Tool.Extensions;

namespace Monica.Framework.Core.Model;

/// <summary>
/// Request class
/// </summary>
/// <param name="type"></param>
public class UnitRequestDto(Type type) : ProjectUnit(type, EProjectUnitType.RequestDto), IHasProjectUnitFactory
{
    static UnitRequestDto()
    {
        AddUnitRegisterFactory(Factory);
    }
   
    public static ProjectUnit? Factory(FactoryContext context)
    {
        var type = context.Type;
        var unit = new UnitRequestDto(type);
        if (!type.IsImplementInterface(typeof(IMoRequestBase))) return null;
        unit.CheckNameConventionMode();
        return unit;
    }
}