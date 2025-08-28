namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 构造函数分析上下文
/// </summary>
/// <param name="ParameterType">参数类型</param>
/// <param name="DependentUnit">依赖此参数的项目单元</param>
public record ConstructorAnalysisContext(Type ParameterType, ProjectUnit DependentUnit);