using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoLibrary.DomainDrivenDesign;
using MoLibrary.Framework.Core.Attributes;
using MoLibrary.Framework.Core.Interfaces;
using MoLibrary.Framework.Modules;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.General;

namespace MoLibrary.Framework.Core.Model;

public interface IHasProjectUnitFactory
{
    /// <summary>
    /// 当前项目单元信息建造工厂
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static abstract ProjectUnit? Factory(FactoryContext context);
}

public class FactoryContext
{
    /// <summary>
    /// 当前类型
    /// </summary>
    public required Type Type { get; set; }

    /// <summary>
    /// 服务注册容器
    /// </summary>
    public required IServiceCollection ServiceCollection { get; set; }
}

/// <summary>
/// 项目单元信息
/// </summary>
public abstract class ProjectUnit(Type type, EProjectUnitType unitType)
{
    private static Func<FactoryContext, ProjectUnit?>? _factories;
    internal static ILogger Logger => Option.Logger ?? NullLogger.Instance;
    internal static ModuleFrameworkMonitorOption Option { get; set; } = null!;

    /// <summary>
    /// 初始化方法元数据
    /// </summary>
    protected void InitializeMethods()
    {
        Methods = ProjectUnitMethodHelper.GetPublicMethods(Type);
    }
    /// <summary>
    /// 初始化方法元数据
    /// </summary>
    protected void InitializeMethods<T>()
    {
        Methods = ProjectUnitMethodHelper.GetPublicMethods(Type, typeof(T));
    }

    /// <summary>
    /// 默认命名惯例规则
    /// </summary>
    /// <returns></returns>
    protected virtual UnitNameConventionOption? DefaultConventionOption()
    {
        return null;
    }

    internal UnitNameConventionOption? ConventionOption =>
        Option.ConventionOptions.Dict.TryGetValue(UnitType, out var option) ? option : DefaultConventionOption();

    /// <summary>
    /// 验证类型
    /// </summary>
    /// <returns></returns>
    protected virtual bool VerifyType()
    {
        if (!VerifyTypeConstrain()) return false;
        CheckNameConventionMode();
        return true;
    }

    /// <summary>
    /// 验证命名惯例
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
    /// 验证类型限制
    /// </summary>
    /// <returns></returns>
    protected virtual bool VerifyTypeConstrain()
    {
        return false;
    }

    /// <summary>
    /// 检查命名限制模式
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
                // 添加错误级别告警
                Alerts.Add(new ProjectUnitAlert
                {
                    Level = EAlertLevel.Error,
                    Message = alertMessage,
                    Source = "NamingConvention"
                });
                throw new InvalidOperationException(alertMessage);
            case ENameConventionMode.Warning:
                // 添加警告级别告警
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
    /// 项目单元初始化完毕后。将项目单元间联系起来
    /// </summary>
    public virtual void DoingConnect()
    {

    }

    /// <summary>
    /// 添加工厂
    /// </summary>
    /// <param name="func"></param>
    public static void AddFactory(Func<FactoryContext, ProjectUnit?> func)
    {
        if (_factories != null)
        {
            var oldFunc = _factories;
            _factories = (context) =>
            {
                var unit = func.Invoke(context);
                return unit ?? oldFunc(context);
            };
        }
        else
        {
            _factories = func;
        }
    }

    /// <summary>
    /// 尝试建造项目单元
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static ProjectUnit? CreateUnit(FactoryContext context)
    {
        return _factories?.Invoke(context);
    }

    /// <summary>
    /// 进一步完善项目单元信息，如提取项目单元特性
    /// </summary>
    public virtual void PolishUnitInfo()
    {
        var attributes = Type.GetCustomAttributes(true).OfType<IUnitCachedAttribute>().ToList();
        if (attributes.Count != 0)
        {
            Attributes.AddRange(attributes);
        }

        if (attributes.OfType<UnitInfoAttribute>().FirstOrDefault() is { } info)
        {
            Title = info.Name;
        }
    }

    /// <summary>
    /// 项目单元键值，也即项目单元FullName名
    /// </summary>
    public string Key => Type.FullName!;

    /// <summary>
    /// 项目单元显示名
    /// </summary>
    public string Title { get; set; } = type.Name;

    /// <summary>
    /// 系统类型
    /// </summary>
    public Type Type { get; init; } = type;

    /// <summary>
    /// 项目单元类型
    /// </summary>
    public EProjectUnitType UnitType { get; protected set; } = unitType;

    /// <summary>
    /// 所依赖的项目单元
    /// </summary>
    public HashSet<ProjectUnit> DependencyUnits { get; protected set; } = [];

    /// <summary>
    /// 项目单元特性
    /// </summary>
    public List<IUnitCachedAttribute> Attributes { get; protected set; } = [];
    
    /// <summary>
    /// 告警信息列表
    /// </summary>
    public List<ProjectUnitAlert> Alerts { get; protected set; } = [];
    
    /// <summary>
    /// 项目单元方法列表
    /// </summary>
    public List<ProjectUnitMethod> Methods { get; protected set; } = [];

    /// <summary>
    /// 增加所依赖的单元
    /// </summary>
    /// <param name="unit"></param>
    /// <param name="isTowWayDependency"></param>
    public virtual void AddDependency(ProjectUnit unit, bool isTowWayDependency = true)
    {
        DependencyUnits.Add(unit);
        if (isTowWayDependency) unit.AddDependency(this, false);
    }

    /// <summary>
    /// 获取所依赖的项目单元
    /// </summary>
    /// <typeparam name="TProjectUnit"></typeparam>
    /// <returns></returns>
    public virtual IReadOnlyList<TProjectUnit> FetchDependency<TProjectUnit>() where TProjectUnit : ProjectUnit
    {
        return DependencyUnits.OfType<TProjectUnit>().ToList();
    }

    #region 检测依赖

    /// <summary>
    /// 检测构造函数中的工作单元依赖
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

                // 检查参数类型是否是一个已注册的工作单元
                if (TryFindUnitForType(parameterType, out var dependentUnit))
                {
                    AddDependency(dependentUnit, false);
                    Logger.LogDebug($"{this}检测到构造函数依赖：{dependentUnit}");
                    continue;
                }
            }
        }
    }

    /// <summary>
    /// 尝试根据类型找到对应的工作单元
    /// </summary>
    /// <param name="type">要查找的类型</param>
    /// <param name="unit">找到的工作单元</param>
    /// <returns>是否找到对应的工作单元</returns>
    private static bool TryFindUnitForType(Type type, [NotNullWhen(true)] out ProjectUnit? unit)
    {
        unit = null;

        if (type.FullName == null) return false;

        // 直接通过类型全名查找
        if (ProjectUnitStores.ProjectUnitsByFullName.TryGetValue(type.FullName, out unit))
        {
            return true;
        }

        //// 如果是泛型类型，尝试通过泛型定义查找
        //if (type.IsGenericType)
        //{
        //    var genericTypeDefinition = type.GetGenericTypeDefinition();
        //    if (genericTypeDefinition.FullName != null &&
        //        ProjectUnitStores.ProjectUnitsByFullName.TryGetValue(genericTypeDefinition.FullName, out unit))
        //    {
        //        return true;
        //    }
        //}

        return false;
    }

    #endregion


    public override string ToString()
    {
        return $"ProjectUnit[{UnitType}] - {Title}({Key})";
    }
}



public class DtoProjectUnit
{
    /// <summary>
    /// 项目单元键值，也即项目单元FullName名
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// 项目单元显示名
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// 项目单元类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EProjectUnitType UnitType { get; set; }

    /// <summary>
    /// 所依赖的项目单元
    /// </summary>
    public List<DtoProjectUnitDependency> DependencyUnits { get; set; } = [];

    /// <summary>
    /// 项目单元特性
    /// </summary>
    public List<IUnitCachedAttribute> Attributes { get; set; } = [];
    
    /// <summary>
    /// 告警信息列表
    /// </summary>
    public List<ProjectUnitAlert> Alerts { get; set; } = [];

    /// <summary>
    /// 项目单元方法列表
    /// </summary>
    [JsonIgnore]
    public List<ProjectUnitMethod> Methods { get; set; } = [];
    
    /// <summary>
    /// 被依赖的数量（在数据传输时计算）
    /// </summary>
    public int DependedByCount { get; set; } = 0;
}
public class DtoProjectUnitDependency
{
    /// <summary>
    /// 项目单元键值，也即项目单元FullName名
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// 项目单元显示名
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// 项目单元类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EProjectUnitType UnitType { get; set; }
}