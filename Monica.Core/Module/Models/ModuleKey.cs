namespace Monica.Core.Module.Models;

/// <summary>
/// Unique module identifier. Supports creation from the built-in <see cref="EMoModuleKey"/> enum or from a string.
/// </summary>
public readonly record struct ModuleKey
{
    /// <summary>
    /// String value of the module key.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Indicates whether this is a built-in Monica module.
    /// </summary>
    public bool IsBuiltIn { get; }

    private ModuleKey(string value, bool isBuiltIn, bool isUIModule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
        IsBuiltIn = isBuiltIn;
        IsUIModule = isUIModule;
    }

    /// <summary>
    /// Indicates whether this module is a UI module.
    /// </summary>
    public bool IsUIModule { get; }

    /// <summary>
    /// Creates a third-party module key. Recommended format: `Vendor.ModuleName`.
    /// </summary>
    public static ModuleKey Create(string key) => new(key, isBuiltIn: false, isUIModule: key.EndsWith("UI"));

    /// <summary>
    /// Implicit conversion from the built-in enum for built-in modules.
    /// </summary>
    public static implicit operator ModuleKey(EMoModuleKey moduleKey) =>
        new(moduleKey.ToString(), isBuiltIn: true, isUIModule: moduleKey.ToString().EndsWith("UI"));

    /// <summary>
    /// Implicit conversion from string for third-party modules.
    /// </summary>
    public static implicit operator ModuleKey(string key) => Create(key);

    /// <summary>
    /// Implicit conversion to string.
    /// </summary>
    public static implicit operator string(ModuleKey key) => key.Value;

    public override string ToString() => Value;
}
