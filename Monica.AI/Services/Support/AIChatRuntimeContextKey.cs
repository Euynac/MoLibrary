namespace Monica.AI.Services.Support;

/// <summary>
/// Strongly typed key used to store module-owned values in an AI chat runtime context.
/// </summary>
/// <typeparam name="T">Value type associated with this key.</typeparam>
public sealed record AIChatRuntimeContextKey<T>(string Name)
{
    /// <summary>
    /// Gets the stable key name. Use a module-qualified name such as <c>rag.knowledge-selection</c>.
    /// </summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(Name)
        ? throw new ArgumentException("Runtime context key name cannot be empty.", nameof(Name))
        : Name;
}
