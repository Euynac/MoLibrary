using Microsoft.Extensions.Logging;
using MoLibrary.DomainDrivenDesign;
using MoLibrary.DomainDrivenDesign.Interfaces;
using MoLibrary.Framework.Core.Interfaces;
using MoLibrary.Framework.Modules;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 应用服务
/// </summary>
/// <param name="type"></param>
public class UnitApplicationService(Type type) : ProjectUnit(type, EProjectUnitType.ApplicationService), IHasProjectUnitFactory
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

    public Type? RequestType { get; set; }
    public Type? ResponseType { get; set; }

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

        unit = unit.VerifyType() ? unit : null;
        if (unit != null)
        {
            if (context.Type.IsSubclassOfRawGeneric(typeof(MoApplicationService<,,>), out var exactGenericType))
            {
                var args = exactGenericType.GetGenericArguments();
                unit.RequestType = args[1];
                unit.ResponseType = args[2];
            }
        }


        return unit;
    }

    public override void DoingConnect()
    {
        // 处理现有的请求类型依赖
        if (RequestType is not null)
        {
            if (!ProjectUnitStores.ProjectUnitsByFullName.TryGetValue(RequestType.FullName!, out var requestUnit))
            {
                var alertMessage = $"{this}无法关联其请求{RequestType.GetCleanFullName()},可能未继承{nameof(IMoRequest)}相关接口";
                // 添加警告级别告警
                Alerts.Add(new ProjectUnitAlert
                {
                    Level = EAlertLevel.Warning,
                    Message = alertMessage,
                    Source = "RequestTypeAssociation"
                });
                Logger.LogWarning(alertMessage);
            }
            else
            {
                DeclareRelevance(requestUnit, true);
                requestUnit.DeclareRelevance(this);
            }
        }

        // 检测构造函数中的工作单元依赖
        DetectConstructorUnitDependencies();
    }
}