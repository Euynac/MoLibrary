using System.Reflection;

namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 项目单元方法元数据模型
/// </summary>
public class ProjectUnitMethod
{
    /// <summary>
    /// 方法定义信息
    /// </summary>
    public required MethodInfo MethodInfo { get; set; }
    
    /// <summary>
    /// 方法名
    /// </summary>
    public string MethodName => MethodInfo.Name;
    
    /// <summary>
    /// 方法描述（从XML注释中获取，如果没有则为空）
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// 已产生的异常数量（后续完善，默认为空）
    /// </summary>
    public int? ExceptionCount { get; set; }
    
    /// <summary>
    /// 方法平均耗时统计（毫秒）（后续完善，默认为空）
    /// </summary>
    public double? AverageExecutionTimeMs { get; set; }
    
    /// <summary>
    /// 方法签名
    /// </summary>
    public string MethodSignature => GetMethodSignature();
    
    /// <summary>
    /// 方法参数信息
    /// </summary>
    public ParameterInfo[] Parameters => MethodInfo.GetParameters();
    
    /// <summary>
    /// 返回类型
    /// </summary>
    public Type ReturnType => MethodInfo.ReturnType;
    
    /// <summary>
    /// 获取方法签名字符串
    /// </summary>
    /// <returns></returns>
    private string GetMethodSignature()
    {
        var parameters = Parameters;
        var parameterStrings = parameters.Select(p => $"{p.ParameterType.Name} {p.Name}");
        return $"{ReturnType.Name} {MethodName}({string.Join(", ", parameterStrings)})";
    }
    
    public override string ToString()
    {
        return $"{MethodSignature} - {Description ?? "无描述"}";
    }
}