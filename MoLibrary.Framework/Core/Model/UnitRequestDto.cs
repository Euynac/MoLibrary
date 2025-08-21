using MoLibrary.DomainDrivenDesign.Interfaces;
using MoLibrary.Framework.Core.Interfaces;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 请求类
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