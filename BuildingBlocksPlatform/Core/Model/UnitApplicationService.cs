using BuildingBlocksPlatform.SeedWork;
using MoLibrary.DomainDrivenDesign;

namespace BuildingBlocksPlatform.Core.Model;

/// <summary>
/// 应用服务
/// </summary>
/// <param name="type"></param>
public class UnitApplicationService(Type type): ProjectUnit(type, EProjectUnitType.ApplicationService) , IHasProjectUnitFactory
{
    static UnitApplicationService()
    {
        AddFactory(Factory);
    }

    /// <summary>
    /// 是否禁用（一般用于测试，关闭该接口使用）
    /// </summary>
    [Obsolete("暂未实现，需先实现自动生成HTTP接口")]
    public bool IsDisabled { get; set; }

    /// <summary>
    /// 是否是写操作
    /// </summary>
    public bool IsCommand { get; set; }

    /// <summary>
    /// 是否是读操作
    /// </summary>
    public bool IsQuery => IsCommand == false;

    protected override bool VerifyTypeConstrain()
    {
        return Type.IsClass && Type.IsSubclassOf(typeof(MoApplicationService));
    }

    protected override UnitNameConventionOption? DefaultConventionOption()
    {
        return new UnitNameConventionOption
        {
            Contains = "Handler"
        };
    }

    public static ProjectUnit? Factory(FactoryContext context)
    {
        var unit = new UnitApplicationService(context.Type)
        {
            IsCommand = context.Type.Name.StartsWith("Command")
        };
        return unit.VerifyType() ? unit : null;
    }
}