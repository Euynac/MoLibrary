namespace Monica.AI.Services.Support;

/// <summary>
/// Provides access to the ambient runtime context for the current AI chat invocation.
/// </summary>
public interface IAIChatRuntimeContextAccessor
{
    /// <summary>
    /// Gets the context visible to currently executing skills and tools.
    /// </summary>
    AIChatRuntimeContext Current { get; }
}
