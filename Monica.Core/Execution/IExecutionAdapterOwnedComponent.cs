namespace Monica.Core.Execution;

/// <summary>
/// Marks a component contract whose invocations already enter Monica's execution pipeline through a module-owned
/// adapter.
/// </summary>
/// <remarks>
/// The optional DynamicProxy execution bridge excludes implementations of this contract to prevent the same operation
/// from entering the pipeline twice. Application service contracts should implement this marker only when their owning
/// module provides the execution boundary.
/// </remarks>
public interface IExecutionAdapterOwnedComponent;
