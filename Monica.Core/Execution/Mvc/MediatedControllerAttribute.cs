namespace Monica.Core.Execution.Mvc;

/// <summary>
/// Marks an HTTP controller or action that delegates business execution to Monica Mediator.
/// MVC execution-pipeline adapters use this marker to avoid applying business behaviors twice.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class MediatedControllerAttribute : Attribute;
