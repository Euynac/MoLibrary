using Microsoft.Extensions.Logging;
using Monica.Modules;
using Monica.ProjectUnits.Abstractions;
using Monica.ProjectUnits.Annotations;
using Monica.ProjectUnits.Services.Support;
using Monica.Tool.Extensions;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Describes a discovered architectural unit and its relationships within one Monica host.
/// </summary>
public abstract class ProjectUnit
{
    private readonly ProjectUnitCatalog _catalog;

    private protected ProjectUnit(Type type, EProjectUnitType unitType, ProjectUnitCatalog catalog)
    {
        Type = type;
        UnitType = unitType;
        Title = type.Name;
        _catalog = catalog;
    }

    private protected ProjectUnitCatalog Catalog => _catalog;

    private protected ILogger Logger => _catalog.Logger;

    /// <summary>
    /// Gets the naming rule configured for this unit type, falling back to the unit's default convention.
    /// </summary>
    internal ProjectUnitNamingRule? ConventionOption =>
        _catalog.Options.ConventionOptions.Dict.TryGetValue(UnitType, out var option)
            ? option
            : DefaultConventionOption();

    /// <summary>
    /// Gets the stable project-unit key, which is the represented CLR type's full name.
    /// </summary>
    public string Key => Type.FullName!;

    /// <summary>
    /// Gets or sets the project-unit display name.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the project-unit description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the project-unit author.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Gets or sets the project-unit groups.
    /// </summary>
    public List<string>? Group { get; set; }

    /// <summary>
    /// Gets the represented CLR type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the architectural category assigned to the unit.
    /// </summary>
    public EProjectUnitType UnitType { get; protected set; }

    /// <summary>
    /// Gets the project units on which this unit depends.
    /// </summary>
    public HashSet<ProjectUnit> DependencyUnits { get; protected set; } = [];

    /// <summary>
    /// Gets cached developer annotations discovered on the represented type.
    /// </summary>
    public List<IUnitCachedAttribute> Attributes { get; protected set; } = [];

    /// <summary>
    /// Gets architecture alerts raised while analyzing the unit.
    /// </summary>
    public List<ProjectUnitAlert> Alerts { get; protected set; } = [];

    /// <summary>
    /// Gets public methods discovered for the unit.
    /// </summary>
    public List<ProjectUnitMethod> Methods { get; protected set; } = [];

    /// <summary>
    /// Gets the parameter types declared by the unit's primary public constructor.
    /// </summary>
    public List<Type> ConstructorParameterTypes { get; protected set; } = [];

    /// <summary>
    /// Connects this unit to other units after discovery has completed.
    /// </summary>
    public virtual void DoingConnect()
    {
        if (ShouldAnalyzeConstructorDependencies)
        {
            DetectConstructorUnitDependencies();
        }
    }

    /// <summary>
    /// Gets whether constructor dependencies should participate in project-unit analysis.
    /// </summary>
    protected virtual bool ShouldAnalyzeConstructorDependencies => false;

    /// <summary>
    /// Enriches the unit with attributes, documentation, and constructor metadata.
    /// </summary>
    public virtual void PolishUnitInfo()
    {
        var attributes = Type.GetCustomAttributes(true).OfType<IUnitCachedAttribute>().ToList();
        if (attributes.Count != 0)
        {
            Attributes.AddRange(attributes);
        }

        InitializeClassInfo();
        if (attributes.OfType<UnitInfoAttribute>().FirstOrDefault() is { } info)
        {
            Title = info.Name;
            Description = info.Description;
            Author = info.Author;
            Group = info.Group?.ToList();
        }
    }

    /// <summary>
    /// Records a relationship between this unit and another unit.
    /// </summary>
    /// <param name="unit">The related project unit.</param>
    /// <param name="isDependent">
    /// <see langword="true"/> when this unit depends on <paramref name="unit"/>; otherwise the relationship is
    /// informational and is handled by a specialized unit model.
    /// </param>
    public virtual void DeclareRelevance(ProjectUnit unit, bool isDependent = false)
    {
        if (isDependent)
        {
            DependencyUnits.Add(unit);
        }
    }

    /// <summary>
    /// Gets dependencies assignable to a specific project-unit model type.
    /// </summary>
    /// <typeparam name="TProjectUnit">The dependency model type to select.</typeparam>
    /// <returns>A snapshot of matching dependencies.</returns>
    public virtual IReadOnlyList<TProjectUnit> FetchDependency<TProjectUnit>() where TProjectUnit : ProjectUnit
    {
        return [.. DependencyUnits.OfType<TProjectUnit>()];
    }

    /// <summary>
    /// Initializes public method metadata for the represented type.
    /// </summary>
    protected void InitializeMethods()
    {
        Methods = _catalog.Documentation.GetPublicMethods(Type);
    }

    /// <summary>
    /// Initializes public method metadata while excluding methods declared by <typeparamref name="TBaseType"/>.
    /// </summary>
    /// <typeparam name="TBaseType">The base type whose declared methods should be excluded.</typeparam>
    protected void InitializeMethods<TBaseType>()
    {
        Methods = _catalog.Documentation.GetPublicMethods(Type, typeof(TBaseType));
    }

    /// <summary>
    /// Gets the default naming convention for this unit model.
    /// </summary>
    /// <returns>The default convention, or <see langword="null"/> when no default applies.</returns>
    protected virtual ProjectUnitNamingRule? DefaultConventionOption()
    {
        return null;
    }

    /// <summary>
    /// Validates type constraints and the configured naming convention.
    /// </summary>
    /// <returns><see langword="true"/> when the represented type belongs to this unit category.</returns>
    protected virtual bool VerifyType()
    {
        if (!VerifyTypeConstrain())
        {
            return false;
        }

        CheckNameConventionMode();
        return true;
    }

    /// <summary>
    /// Validates the configured naming convention.
    /// </summary>
    /// <returns><see langword="true"/> when the name satisfies the convention or convention checking is disabled.</returns>
    protected virtual bool VerifyNameConvention()
    {
        if (!_catalog.Options.ConventionOptions.EnableNameConvention || ConventionOption is not { } option)
        {
            return true;
        }

        var success = true;
        if (option.Postfix is { } postfix)
        {
            success &= Type.Name.EndsWith(postfix, StringComparison.Ordinal);
        }

        if (success && option.Prefix is { } prefix)
        {
            success &= Type.Name.StartsWith(prefix, StringComparison.Ordinal);
        }

        return success;
    }

    /// <summary>
    /// Determines whether the represented CLR type belongs to this unit category.
    /// </summary>
    /// <returns><see langword="true"/> when the type belongs to this category.</returns>
    protected virtual bool VerifyTypeConstrain()
    {
        return false;
    }

    /// <summary>
    /// Applies the configured naming-convention failure mode.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when strict convention validation fails.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an unsupported convention mode is configured.</exception>
    protected void CheckNameConventionMode()
    {
        if (VerifyNameConvention() || ConventionOption is not { } option)
        {
            return;
        }

        var alertMessage = $"{Type.GetCleanFullName()} must satisfy the naming convention: {option}";

        switch (option.NameConventionMode ?? _catalog.Options.ConventionOptions.NameConventionMode)
        {
            case ENameConventionMode.Strict:
                Alerts.Add(new ProjectUnitAlert
                {
                    Level = EAlertLevel.Error,
                    Message = alertMessage,
                    Source = "NamingConvention"
                });
                throw new InvalidOperationException(alertMessage);
            case ENameConventionMode.Warning:
                Alerts.Add(new ProjectUnitAlert
                {
                    Level = EAlertLevel.Warning,
                    Message = alertMessage,
                    Source = "NamingConvention"
                });
                Logger.LogError("{NamingConventionAlert}", alertMessage);
                break;
            case ENameConventionMode.Disable:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"ProjectUnit[{UnitType}] - {Title}({Key})";
    }

    private void InitializeClassInfo()
    {
        Description = _catalog.Documentation.ExtractTypeDescription(Type);

        var mainConstructor = Type.GetConstructors()
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .FirstOrDefault();
        if (mainConstructor is not null)
        {
            ConstructorParameterTypes = [.. mainConstructor.GetParameters().Select(parameter => parameter.ParameterType)];
        }
    }

    private void DetectConstructorUnitDependencies()
    {
        foreach (var constructor in Type.GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                if (_catalog.ResolveConstructorDependency(parameter.ParameterType, this) is not { } dependentUnit)
                {
                    continue;
                }

                DeclareRelevance(dependentUnit, true);
                dependentUnit.DeclareRelevance(this);
                Logger.LogDebug(
                    "Project unit {ProjectUnitKey} depends on {DependencyUnitKey} through its constructor.",
                    Key,
                    dependentUnit.Key);
            }
        }
    }
}
