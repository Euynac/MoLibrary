using BuildingBlocksPlatform.Core.Attributes;
using BuildingBlocksPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.Core.Model;

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
    internal static MonitorOption Option { get; set; } = null!;

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
        if (Option.ConventionOptions.EnablePerformanceMode && !VerifyNameConvention()) return false;
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
        if(VerifyNameConvention()) return;
        var option = ConventionOption;
        if(option == null) return;
        switch (option.NameConventionMode ?? Option.ConventionOptions.NameConventionMode)
        {
            case ENameConventionMode.Strict:
                throw new InvalidOperationException($"{Type.FullName}需满足命名限制：{option}");
            case ENameConventionMode.Warning:
                Logger.Log(LogLevel.Error, $"{Type.FullName}需满足命名限制：{option}");
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
    public List<ProjectUnit> DependencyUnits { get; protected set; } = [];

    /// <summary>
    /// 项目单元特性
    /// </summary>
    public List<IUnitCachedAttribute> Attributes { get; protected set; } = [];

    /// <summary>
    /// 增加所依赖的单元
    /// </summary>
    /// <param name="unit"></param>
    public virtual void AddDependency(ProjectUnit unit)
    {
        DependencyUnits.Add(unit);
    }

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
    public string Key { get; set; }

    /// <summary>
    /// 项目单元显示名
    /// </summary>
    public string Title { get; set; }

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

}
public class DtoProjectUnitDependency
{
    /// <summary>
    /// 项目单元键值，也即项目单元FullName名
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// 项目单元显示名
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 项目单元类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EProjectUnitType UnitType { get; set; }
}