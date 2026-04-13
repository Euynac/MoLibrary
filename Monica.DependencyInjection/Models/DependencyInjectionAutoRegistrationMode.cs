namespace Monica.DependencyInjection.Models;

/// <summary>
/// Describes which registration mode Monica conventional registration used.
/// </summary>
public enum DependencyInjectionAutoRegistrationMode
{
    /// <summary>
    /// The descriptor was appended directly to the collection.
    /// </summary>
    Add,

    /// <summary>
    /// The descriptor was added only when no matching service identity already existed.
    /// </summary>
    TryAdd,

    /// <summary>
    /// The descriptor replaced an existing matching service identity when present.
    /// </summary>
    Replace
}
