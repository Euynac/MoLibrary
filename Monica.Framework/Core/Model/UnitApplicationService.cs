using Microsoft.Extensions.Logging;
using Monica.DomainDrivenDesign;
using Monica.DomainDrivenDesign.Interfaces;
using Monica.Framework.Core.Interfaces;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.Framework.Core.Model;

/// <summary>
/// application services
/// </summary>
/// <param name="type"></param>
public class UnitApplicationService(Type type) : ProjectUnit(type, EProjectUnitType.ApplicationService), IHasProjectUnitFactory
{
    static UnitApplicationService()
    {
        AddUnitRegisterFactory(Factory);
    }

    /// <summary>
    /// Whether to disable (generally used for testing, close the interface for use)
    /// </summary>
    [Obsolete("暂未实现，需先实现自动生成HTTP接口")]
    public bool IsDisabled { get; set; }

    /// <summary>
    /// Is it a write operation?
    /// </summary>
    public bool IsCommand { get; set; }

    /// <summary>
    /// Is it a read operation?
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
            if (context.Type.IsSubclassOfRawGeneric(typeof(MoApplicationService<,>), out var exactGenericType))
            {
                var args = exactGenericType.GetGenericArguments();
                unit.RequestType = args[0];
                unit.ResponseType = args[1];
            }
        }


        return unit;
    }

    public override void DoingConnect()
    {
        // Handle existing request type dependencies
        if (RequestType is not null)
        {
            if (!ProjectUnitStores.ProjectUnitsByFullName.TryGetValue(RequestType.FullName!, out var requestUnit))
            {
                var alertMessage = $"{this}无法关联其请求{RequestType.GetCleanFullName()},可能未继承{nameof(IMoRequest)}相关接口";
                // Add warning level alert
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

        // Detecting unit-of-work dependencies in constructors
        DetectConstructorUnitDependencies();
    }
}
