using Microsoft.Extensions.Logging;
using Monica.Core.Mediator;
using Monica.ProjectUnits.Services.Support;
using Monica.Modules;
using Monica.Tool.Extensions;
using Monica.WebApi.Abstractions;
using Monica.WebApi.AutoControllers.Abstractions;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Describes an application service, its request contract, and its architectural dependencies.
/// </summary>
public class UnitApplicationService : ProjectUnit
{
    internal UnitApplicationService(Type type, ProjectUnitCatalog catalog)
        : base(type, EProjectUnitType.ApplicationService, catalog, MediatorExecutionPoints.Request)
    {
    }

    /// <summary>
    /// Gets or sets whether generated HTTP exposure should be disabled when endpoint generation is implemented.
    /// </summary>
    [Obsolete("Not implemented. Automatic HTTP endpoint generation is required before this flag can take effect.")]
    public bool IsDisabled { get; set; }

    /// <summary>
    /// Gets or sets whether the application service represents a write operation.
    /// </summary>
    public bool IsCommand { get; set; }

    /// <summary>
    /// Gets whether the application service represents a read operation.
    /// </summary>
    public bool IsQuery => IsCommand == false;

    /// <summary>
    /// Gets or sets the request type accepted by a generic application service.
    /// </summary>
    public Type? RequestType { get; set; }

    /// <summary>
    /// Gets or sets the response type returned by a generic application service.
    /// </summary>
    public Type? ResponseType { get; set; }

    protected override bool ShouldAnalyzeConstructorDependencies => true;

    protected override bool VerifyTypeConstrain()
    {
        // CRUD application services are claimed by UnitCrudApplicationService, even though they also derive from
        // ApplicationService. Exclude them here so each type maps to exactly one project unit.
        return Type.IsClass
               && Type.IsSubclassOf(typeof(ApplicationService))
               && !Type.IsImplementInterface<ICrudApplicationService>();
    }

    protected override ProjectUnitNamingRule? DefaultConventionOption()
    {
        return new ProjectUnitNamingRule
        {
            Contains = "Handler"
        };
    }

    internal static ProjectUnit? Create(Type type, ProjectUnitCatalog catalog)
    {
        var unit = new UnitApplicationService(type, catalog)
        {
            IsCommand = type.Name.StartsWith("Command", StringComparison.Ordinal)
        };

        unit = unit.VerifyType() ? unit : null;
        if (unit != null)
        {
            if (type.IsSubclassOfRawGeneric(typeof(ApplicationService<,>), out var exactGenericType))
            {
                var args = exactGenericType.GetGenericArguments();
                unit.RequestType = args[0];
                unit.ResponseType = args[1];
            }

            unit.InitializeMethods<ApplicationService>();
        }


        return unit;
    }

    public override void DoingConnect()
    {
        if (RequestType is not null)
        {
            if (Catalog.FindByFullName(RequestType.FullName) is not { } requestUnit)
            {
                var alertMessage =
                    $"Application-service project unit {this} could not associate request type " +
                    $"{RequestType.GetCleanFullName()} because no request project unit was discovered. Ensure the " +
                    $"request implements a supported {nameof(IResultRequest)} contract and is included in the host's " +
                    "business-type discovery.";
                Alerts.Add(new ProjectUnitAlert
                {
                    Level = EAlertLevel.Warning,
                    Message = alertMessage,
                    Source = "RequestTypeAssociation"
                });
                Logger.LogWarning("{RequestTypeAssociationAlert}", alertMessage);
            }
            else
            {
                DeclareRelevance(requestUnit, true);
                requestUnit.DeclareRelevance(this);
            }
        }

        base.DoingConnect();
    }
}
