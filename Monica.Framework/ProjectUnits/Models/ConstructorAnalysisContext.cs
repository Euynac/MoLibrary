namespace Monica.Framework.ProjectUnits.Models;

/// <summary>
/// Constructor analysis context
/// </summary>
/// <param name="ParameterType">Parameter type</param>
/// <param name="DependentUnit">Project units that depend on this parameter</param>
public record ConstructorAnalysisContext(Type ParameterType, ProjectUnit DependentUnit);