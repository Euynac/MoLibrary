using Microsoft.Extensions.Logging;
using Monica.Framework.Core.Attributes;
using Monica.Framework.Core.Interfaces;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.Framework.Core.Model;

/// <summary>
/// Project unit information
/// </summary>
public abstract class ProjectUnit(Type type, EProjectUnitType unitType)
{
    private static Func<FactoryContext, ProjectUnit?>? _unitRegisterFactories;
    private static Func<ConstructorAnalysisContext, ProjectUnit?> _constructorAnalyzerFactories = ConstructorDefaultAnalyzerFactory;

    /// <summary>
    /// By default, the project unit is searched by the full name of the type.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    private static ProjectUnit? ConstructorDefaultAnalyzerFactory(ConstructorAnalysisContext context)
    {
        if (context.ParameterType.FullName == null) return null;

        // Search directly by type full name
        return ProjectUnitStores.ProjectUnitsByFullName.TryGetValue(context.ParameterType.FullName, out var unit) ? unit : null;
    }
   
    internal static ILogger Logger => Option.Logger;
    internal static ModuleFrameworkMonitorOption Option { get; set; } = null!;

    /// <summary>
    /// Initialize class metadata
    /// </summary>
    private void InitializeClassInfo()
    {
        Description = ProjectUnitXmlDocHelper.ExtractTypeDescription(Type);
        InitializeConstructorParameterTypes();
    }

    /// <summary>
    /// Initialize constructor parameter type information
    /// </summary>
    private void InitializeConstructorParameterTypes()
    {
        var constructors = Type.GetConstructors();
        
        // Choose the constructor with the most parameters (usually the primary constructor)
        var mainConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
        
        if (mainConstructor != null)
        {
            var parameters = mainConstructor.GetParameters();
            ConstructorParameterTypes = parameters.Select(p => p.ParameterType).ToList();
        }
    }
    /// <summary>
    /// Initialization method metadata
    /// </summary>
    protected void InitializeMethods()
    {
        Methods = ProjectUnitXmlDocHelper.GetPublicMethods(Type);
    }
    /// <summary>
    /// Initialization method metadata
    /// </summary>
    protected void InitializeMethods<T>()
    {
        Methods = ProjectUnitXmlDocHelper.GetPublicMethods(Type, typeof(T));
    }

    /// <summary>
    /// Default naming convention rules
    /// </summary>
    /// <returns></returns>
    protected virtual UnitNameConventionOption? DefaultConventionOption()
    {
        return null;
    }

    internal UnitNameConventionOption? ConventionOption =>
        Option.ConventionOptions.Dict.TryGetValue(UnitType, out var option) ? option : DefaultConventionOption();

    /// <summary>
    /// Validating Types: Type Restrictions and Naming Conventions
    /// </summary>
    /// <returns></returns>
    protected virtual bool VerifyType()
    {
        if (!VerifyTypeConstrain()) return false;
        CheckNameConventionMode();
        return true;
    }

    /// <summary>
    /// Verify naming convention
    /// </summary>
    /// <returns></returns>
    protected virtual bool VerifyNameConvention()
    {
        if (!Option.ConventionOptions.EnableNameConvention || ConventionOption is not {} option) return true;
        var success = true;
        if (option.Postfix is { } postfix)
        {
            success &= Type.Name.EndsWith(postfix);
        }
        if(success && option.Prefix is {} prefix)
        {
            success &= Type.Name.StartsWith(prefix);
        }
        return success;
    }

    /// <summary>
    /// Validation type restrictions
    /// </summary>
    /// <returns></returns>
    protected virtual bool VerifyTypeConstrain()
    {
        return false;
    }

    /// <summary>
    /// Check naming restriction pattern
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    protected virtual void CheckNameConventionMode()
    {
        if (VerifyNameConvention()) return;
        var option = ConventionOption;
        if(option == null) return;
        
        var alertMessage = $"{Type.GetCleanFullName()}需满足命名限制：{option}";
        
        switch (option.NameConventionMode ?? Option.ConventionOptions.NameConventionMode)
        {
            case ENameConventionMode.Strict:
                // Add error level alert
                Alerts.Add(new ProjectUnitAlert
                {
                    Level = EAlertLevel.Error,
                    Message = alertMessage,
                    Source = "NamingConvention"
                });
                throw new InvalidOperationException(alertMessage);
            case ENameConventionMode.Warning:
                // Add warning level alert
                Alerts.Add(new ProjectUnitAlert
                {
                    Level = EAlertLevel.Warning,
                    Message = alertMessage,
                    Source = "NamingConvention"
                });
                Logger.Log(LogLevel.Error, alertMessage);
                break;
            case ENameConventionMode.Disable:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }



    /// <summary>
    /// After the project unit is initialized. Connect project units
    /// </summary>
    public virtual void DoingConnect()
    {

    }
    /// <summary>
    /// Add unit constructor dependency resolution factory
    /// </summary>
    /// <param name="func"></param>
    public static void AddConstructorAnalyzerFactory(Func<ConstructorAnalysisContext, ProjectUnit?> func)
    {
        if (_constructorAnalyzerFactories != null)
        {
            var oldFunc = _constructorAnalyzerFactories;
            _constructorAnalyzerFactories = (context) =>
            {
                var unit = oldFunc.Invoke(context);
                return unit ?? func(context);
            };
        }
        else
        {
            _constructorAnalyzerFactories = func;
        }
    }
    /// <summary>
    /// Add unit registration factory
    /// </summary>
    /// <param name="func"></param>
    public static void AddUnitRegisterFactory(Func<FactoryContext, ProjectUnit?> func)
    {
        if (_unitRegisterFactories != null)
        {
            var oldFunc = _unitRegisterFactories;
            _unitRegisterFactories = (context) =>
            {
                var unit = func.Invoke(context);
                return unit ?? oldFunc(context);
            };
        }
        else
        {
            _unitRegisterFactories = func;
        }
    }

    /// <summary>
    /// Try building a project unit
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static ProjectUnit? CreateUnit(FactoryContext context)
    {
        return _unitRegisterFactories?.Invoke(context);
    }

    /// <summary>
    /// Further improve project unit information, such as extracting project unit characteristics
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
    /// Project unit key value, that is, project unit FullName name
    /// </summary>
    public string Key => Type.FullName!;

    /// <summary>
    /// Project unit display name
    /// </summary>
    public string Title { get; set; } = type.Name;

    /// <summary>
    /// Project unit description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Project unit author
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Project unit grouping information
    /// </summary>
    public List<string>? Group { get; set; }

    /// <summary>
    /// System type
    /// </summary>
    public Type Type { get; init; } = type;

    /// <summary>
    /// Project unit type
    /// </summary>
    public EProjectUnitType UnitType { get; protected set; } = unitType;

    /// <summary>
    /// The project unit it depends on
    /// </summary>
    public HashSet<ProjectUnit> DependencyUnits { get; protected set; } = [];

    /// <summary>
    /// Project unit properties
    /// </summary>
    public List<IUnitCachedAttribute> Attributes { get; protected set; } = [];
    
    /// <summary>
    /// Alarm information list
    /// </summary>
    public List<ProjectUnitAlert> Alerts { get; protected set; } = [];
    
    /// <summary>
    /// Project unit method list
    /// </summary>
    public List<ProjectUnitMethod> Methods { get; protected set; } = [];

    /// <summary>
    /// Constructor parameter information list
    /// </summary>
    public List<Type> ConstructorParameterTypes { get; protected set; } = [];

    /// <summary>
    /// Declare project unit dependencies
    /// </summary>
    /// <param name="unit"></param>
    /// <param name="isDependent"></param>
    public virtual void DeclareRelevance(ProjectUnit unit, bool isDependent = false)
    {
        if (isDependent)
        {
            DependencyUnits.Add(unit);
        }
        
    }

    /// <summary>
    /// Get the dependent project units
    /// </summary>
    /// <typeparam name="TProjectUnit"></typeparam>
    /// <returns></returns>
    public virtual IReadOnlyList<TProjectUnit> FetchDependency<TProjectUnit>() where TProjectUnit : ProjectUnit
    {
        return DependencyUnits.OfType<TProjectUnit>().ToList();
    }

    #region 检测依赖

    /// <summary>
    /// Detect project unit dependencies in constructor
    /// </summary>
    protected void DetectConstructorUnitDependencies()
    {
        var constructors = Type.GetConstructors();

        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();

            foreach (var parameter in parameters)
            {
                var parameterType = parameter.ParameterType;
                var context = new ConstructorAnalysisContext(parameterType, this);

                // Check if the parameter type is a registered project unit
                if (_constructorAnalyzerFactories(context) is {} dependentUnit)
                {
                    DeclareRelevance(dependentUnit, true);
                    dependentUnit.DeclareRelevance(this);
                    Logger.LogDebug($"{this}检测到构造函数依赖：{dependentUnit}");
                }
            }
        }
    }

  

    #endregion


    public override string ToString()
    {
        return $"ProjectUnit[{UnitType}] - {Title}({Key})";
    }
}