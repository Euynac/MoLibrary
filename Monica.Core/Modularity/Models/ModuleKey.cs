namespace Monica.Core.Modularity.Models;

/// <summary>
/// Provides a stable display label derived from a Monica module strategy type.
/// </summary>
/// <remarks>
/// Module composition and capability checks use CLR <see cref="Type"/> identity. This value is diagnostic metadata
/// only and must not be used as a second module identity system.
/// </remarks>
public readonly struct ModuleKey : IEquatable<ModuleKey>
{
    private readonly Type? _moduleType;
    private readonly string? _id;
    private readonly string? _value;

    /// <summary>
    /// Gets the stable, assembly-qualified diagnostic identity used by browser state and portable exports.
    /// </summary>
    /// <remarks>
    /// The identity combines the defining assembly's simple name with the full CLR type name. It is stable across
    /// assembly version changes while avoiding collisions between equal type names published by different assemblies.
    /// CLR <see cref="Type"/> identity remains authoritative for the live module graph.
    /// </remarks>
    public string Id => _id ?? string.Empty;

    /// <summary>
    /// Gets the human-readable module type name.
    /// </summary>
    /// <remarks>
    /// This display value is not required to be globally unique. Equality retains the projected CLR type identity so
    /// equal full names from different assemblies or load contexts remain distinct diagnostic keys.
    /// </remarks>
    public string Value => _value ?? string.Empty;

    private ModuleKey(Type moduleType, string id, string value)
    {
        _moduleType = moduleType;
        _id = id;
        _value = value;
    }

    /// <summary>
    /// Creates diagnostic metadata from a module CLR type. CLR type identity, rather than this value,
    /// is authoritative for composition.
    /// </summary>
    public static ModuleKey FromModuleType(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);
        var value = moduleType.FullName ?? moduleType.Name;
        var assemblyName = moduleType.Assembly.GetName().Name
                           ?? moduleType.Assembly.FullName
                           ?? "unknown-assembly";
        return new ModuleKey(moduleType, $"{assemblyName}::{value}", value);
    }

    /// <inheritdoc />
    public bool Equals(ModuleKey other) =>
        _moduleType == other._moduleType;

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is ModuleKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        _moduleType?.GetHashCode() ?? 0;

    /// <summary>
    /// Determines whether two module keys identify the same module.
    /// </summary>
    public static bool operator ==(ModuleKey left, ModuleKey right) => left.Equals(right);

    /// <summary>
    /// Determines whether two module keys identify different modules.
    /// </summary>
    public static bool operator !=(ModuleKey left, ModuleKey right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => Value;

}
