using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Annotations;

/// <summary>
/// Declares the static <see cref="ModuleKey"/> metadata for a module type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ModuleKeyAttribute : Attribute
{
    /// <summary>
    /// Gets the validated module identity declared by the annotated type.
    /// </summary>
    public ModuleKey Key { get; }

    /// <summary>
    /// Declares an official Monica module identity.
    /// </summary>
    /// <param name="key">The built-in identity reserved by the Monica framework.</param>
    public ModuleKeyAttribute(BuiltInModuleKey key)
    {
        Key = key;
    }

    /// <summary>
    /// Declares an independently published Monica ecosystem module identity.
    /// </summary>
    /// <param name="key">
    /// The publisher-first identity in the form
    /// <c>&lt;Publisher&gt;.Monica.&lt;Module&gt;[.&lt;Feature&gt;...]</c>.
    /// </param>
    /// <exception cref="ArgumentException">The supplied identity does not follow the ecosystem grammar.</exception>
    public ModuleKeyAttribute(string key)
    {
        Key = ModuleKey.Create(key);
    }
}
