namespace Monica.Core.Modularity.Models;

/// <summary>
/// Identifies a Monica module independently of its CLR type.
/// </summary>
/// <remarks>
/// Module-key identity is case-insensitive while <see cref="Value"/> preserves the casing supplied by the publisher.
/// </remarks>
public readonly struct ModuleKey : IEquatable<ModuleKey>
{
    private const int MAXIMUM_KEY_LENGTH = 100;
    private const string MONICA_SEGMENT = "Monica";
    private readonly string? _value;

    /// <summary>
    /// String value of the module key.
    /// </summary>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Indicates whether this is a built-in Monica module.
    /// </summary>
    public bool IsBuiltIn { get; }

    private ModuleKey(string value, bool isBuiltIn, bool isUIModule)
    {
        _value = value;
        IsBuiltIn = isBuiltIn;
        IsUIModule = isUIModule;
    }

    /// <summary>
    /// Indicates whether this module is a UI module.
    /// </summary>
    public bool IsUIModule { get; }

    /// <summary>
    /// Creates a third-party module key aligned with a Monica ecosystem package identifier.
    /// </summary>
    /// <param name="key">
    /// A key in the form <c>&lt;Publisher&gt;.Monica.&lt;Module&gt;[.&lt;Feature&gt;...]</c>. Every segment must begin
    /// with an ASCII letter and contain only ASCII letters or digits.
    /// </param>
    /// <returns>The validated third-party module key.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> does not follow the ecosystem format.</exception>
    public static ModuleKey Create(string key)
    {
        ValidateThirdPartyKey(key);
        return new ModuleKey(key, isBuiltIn: false, isUIModule: HasExternalUISegment(key));
    }

    /// <summary>
    /// Implicit conversion from the built-in enum for built-in modules.
    /// </summary>
    public static implicit operator ModuleKey(BuiltInModuleKey moduleKey)
    {
        var value = moduleKey.ToString();
        return new ModuleKey(value, isBuiltIn: true, isUIModule: value.EndsWith("UI", StringComparison.Ordinal));
    }

    /// <summary>
    /// Implicit conversion from string for third-party modules.
    /// </summary>
    public static implicit operator ModuleKey(string key) => Create(key);

    /// <summary>
    /// Implicit conversion to string.
    /// </summary>
    public static implicit operator string(ModuleKey key) => key.Value;

    /// <inheritdoc />
    public bool Equals(ModuleKey other) =>
        StringComparer.OrdinalIgnoreCase.Equals(Value, other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is ModuleKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

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

    private static void ValidateThirdPartyKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (key.Length > MAXIMUM_KEY_LENGTH)
        {
            throw new ArgumentException(
                $"A module key cannot exceed {MAXIMUM_KEY_LENGTH} characters.",
                nameof(key));
        }

        var segments = key.Split('.');
        if (segments.Length < 3 ||
            segments[0].Equals(MONICA_SEGMENT, StringComparison.OrdinalIgnoreCase) ||
            !segments[1].Equals(MONICA_SEGMENT, StringComparison.Ordinal) ||
            segments.Any(static segment => !IsValidSegment(segment)))
        {
            throw new ArgumentException(
                "A third-party module key must use the format " +
                "'<Publisher>.Monica.<Module>[.<Feature>...]' with ASCII letter-or-digit segments. " +
                "Each segment must begin with a letter, the framework segment must use canonical 'Monica' casing, " +
                "and the 'Monica' publisher is reserved for official modules.",
                nameof(key));
        }
    }

    private static bool IsValidSegment(string segment)
    {
        if (segment.Length == 0 || !IsAsciiLetter(segment[0]))
        {
            return false;
        }

        foreach (var character in segment.AsSpan(1))
        {
            if (!IsAsciiLetter(character) && !char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetter(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool HasExternalUISegment(string key) =>
        key.EndsWith(".UI", StringComparison.OrdinalIgnoreCase);
}
