namespace Monica.Core.Modularity.Models;

/// <summary>
/// Declares the static <see cref="ModuleKey"/> metadata for a module type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ModuleKeyAttribute : Attribute
{
    public ModuleKey Key { get; }

    public ModuleKeyAttribute(EMoModuleKey key)
    {
        Key = key;
    }

    public ModuleKeyAttribute(string key)
    {
        Key = ModuleKey.Create(key);
    }
}
