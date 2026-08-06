using System.Collections.Immutable;
using System.Globalization;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Contains an explicitly allow-listed projection of one module's finalized default options.
/// </summary>
public sealed record ModuleOptionDiagnostics
{
    /// <summary>Gets the module whose options were projected.</summary>
    public required ModuleKey ModuleKey { get; init; }

    /// <summary>Gets the concrete option type name.</summary>
    public required string OptionTypeName { get; init; }

    /// <summary>Gets whether the module publisher or host configured a safe projection.</summary>
    public bool IsConfigured { get; init; }

    /// <summary>Gets the allow-listed option entries in declaration order.</summary>
    public ImmutableArray<ModuleOptionDiagnosticEntry> Entries { get; init; } = [];
}

/// <summary>Describes one safely projected option value.</summary>
public sealed record ModuleOptionDiagnosticEntry
{
    /// <summary>Gets the stable developer-facing option label.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the projection kind.</summary>
    public ModuleOptionDiagnosticValueKind Kind { get; init; }

    /// <summary>Gets the invariant scalar representation for <see cref="ModuleOptionDiagnosticValueKind.Value"/>.</summary>
    public string? Value { get; init; }

    /// <summary>Gets whether a secret or sensitive value is present.</summary>
    public bool? IsPresent { get; init; }

    /// <summary>Gets the bounded collection count.</summary>
    public int? Count { get; init; }

    /// <summary>Gets whether an exposed scalar is explicitly <see langword="null"/>.</summary>
    public bool IsNull { get; init; }

    /// <summary>Gets whether a scalar string was shortened to its configured bound.</summary>
    public bool IsTruncated { get; init; }
}

/// <summary>Defines the only option projections permitted across the diagnostics boundary.</summary>
public enum ModuleOptionDiagnosticValueKind
{
    /// <summary>An explicitly approved bounded scalar value.</summary>
    Value,

    /// <summary>Only the presence of sensitive data is disclosed.</summary>
    Presence,

    /// <summary>Only a non-negative collection count is disclosed.</summary>
    Count,

    /// <summary>The configured getter failed or returned an invalid value.</summary>
    Unavailable
}

/// <summary>
/// Builds an allow-list for one concrete module option type without retaining or returning its live object graph.
/// </summary>
/// <typeparam name="TOptions">The finalized module option type.</typeparam>
public sealed class ModuleOptionDiagnosticsBuilder<TOptions> where TOptions : class
{
    private const int DEFAULT_MAX_VALUE_LENGTH = 256;
    private const int MAX_NAME_LENGTH = 128;
    private const int MAX_VALUE_LENGTH = 1024;
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private readonly List<IModuleOptionDiagnosticDefinition<TOptions>> _definitions = [];

    /// <summary>
    /// Exposes one explicitly approved scalar using invariant formatting and a bounded representation.
    /// </summary>
    /// <typeparam name="TValue">A string, enum, primitive, date/time, duration, GUID, or nullable form.</typeparam>
    /// <param name="name">The stable display label.</param>
    /// <param name="getter">Reads the scalar from the finalized option.</param>
    /// <param name="maxLength">Maximum representation length; defaults to 256 and cannot exceed 1024.</param>
    /// <returns>This builder.</returns>
    public ModuleOptionDiagnosticsBuilder<TOptions> ExposeValue<TValue>(
        string name,
        Func<TOptions, TValue> getter,
        int maxLength = DEFAULT_MAX_VALUE_LENGTH)
    {
        ValidateNameAndGetter(name, getter);
        if (maxLength is < 1 or > MAX_VALUE_LENGTH)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxLength),
                maxLength,
                $"Option diagnostic values must be bounded between 1 and {MAX_VALUE_LENGTH} characters.");
        }

        if (!IsSupportedScalar(typeof(TValue)))
        {
            throw new ArgumentException(
                $"{typeof(TValue).FullName} is not a supported diagnostic scalar. " +
                "Expose only primitive, enum, string, date/time, duration, or GUID values.",
                nameof(getter));
        }

        _definitions.Add(new ScalarOptionDiagnosticDefinition<TOptions, TValue>(name, getter, maxLength));
        return this;
    }

    /// <summary>
    /// Exposes only whether sensitive data such as a token, key, or secret is configured.
    /// </summary>
    /// <param name="name">The stable display label.</param>
    /// <param name="getter">Returns <see langword="true"/> when the sensitive value is present.</param>
    /// <returns>This builder.</returns>
    public ModuleOptionDiagnosticsBuilder<TOptions> ExposePresence(
        string name,
        Func<TOptions, bool> getter)
    {
        ValidateNameAndGetter(name, getter);
        _definitions.Add(new PresenceOptionDiagnosticDefinition<TOptions>(name, getter));
        return this;
    }

    /// <summary>
    /// Exposes only a non-negative collection or configured-item count.
    /// </summary>
    /// <param name="name">The stable display label.</param>
    /// <param name="getter">Returns the bounded count without exposing collection elements.</param>
    /// <returns>This builder.</returns>
    public ModuleOptionDiagnosticsBuilder<TOptions> ExposeCount(
        string name,
        Func<TOptions, int> getter)
    {
        ValidateNameAndGetter(name, getter);
        _definitions.Add(new CountOptionDiagnosticDefinition<TOptions>(name, getter));
        return this;
    }

    internal IModuleOptionDiagnosticsProjection Build(Type moduleType)
    {
        return new ModuleOptionDiagnosticsProjection<TOptions>(moduleType, _definitions.ToArray());
    }

    private void ValidateNameAndGetter<TValue>(string name, Func<TOptions, TValue> getter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(getter);
        if (name.Length > MAX_NAME_LENGTH)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                name.Length,
                $"Option diagnostic names cannot exceed {MAX_NAME_LENGTH} characters.");
        }

        if (!_names.Add(name))
        {
            throw new InvalidOperationException($"Option diagnostic entry '{name}' was declared more than once.");
        }
    }

    private static bool IsSupportedScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsEnum
               || type.IsPrimitive
               || type == typeof(string)
               || type == typeof(decimal)
               || type == typeof(DateOnly)
               || type == typeof(TimeOnly)
               || type == typeof(DateTime)
               || type == typeof(DateTimeOffset)
               || type == typeof(TimeSpan)
               || type == typeof(Guid);
    }
}

internal interface IModuleOptionDiagnosticsProjection
{
    Type ModuleType { get; }

    ModuleOptionDiagnostics Project(ModuleKey moduleKey, object options);
}

internal interface IModuleOptionDiagnosticDefinition<in TOptions>
{
    ModuleOptionDiagnosticEntry Evaluate(TOptions options);
}

internal sealed class ModuleOptionDiagnosticsProjection<TOptions>(
    Type moduleType,
    IReadOnlyList<IModuleOptionDiagnosticDefinition<TOptions>> definitions)
    : IModuleOptionDiagnosticsProjection where TOptions : class
{
    public Type ModuleType { get; } = moduleType;

    public ModuleOptionDiagnostics Project(ModuleKey moduleKey, object options)
    {
        var typedOptions = options as TOptions
            ?? throw new InvalidOperationException(
                $"Module {ModuleType.Name} options are not assignable to {typeof(TOptions).FullName}.");
        return new ModuleOptionDiagnostics
        {
            ModuleKey = moduleKey,
            OptionTypeName = typeof(TOptions).FullName ?? typeof(TOptions).Name,
            IsConfigured = true,
            Entries = definitions.Select(definition => definition.Evaluate(typedOptions)).ToImmutableArray()
        };
    }
}

internal sealed class ScalarOptionDiagnosticDefinition<TOptions, TValue>(
    string name,
    Func<TOptions, TValue> getter,
    int maxLength) : IModuleOptionDiagnosticDefinition<TOptions>
{
    public ModuleOptionDiagnosticEntry Evaluate(TOptions options)
    {
        try
        {
            var value = getter(options);
            if (value is null)
            {
                return new ModuleOptionDiagnosticEntry
                {
                    Name = name,
                    Kind = ModuleOptionDiagnosticValueKind.Value,
                    IsNull = true
                };
            }

            var text = value switch
            {
                DateTime dateTime => dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            } ?? string.Empty;
            var isTruncated = text.Length > maxLength;
            return new ModuleOptionDiagnosticEntry
            {
                Name = name,
                Kind = ModuleOptionDiagnosticValueKind.Value,
                Value = isTruncated ? text[..maxLength] : text,
                IsTruncated = isTruncated
            };
        }
        catch
        {
            return Unavailable(name);
        }
    }

    private static ModuleOptionDiagnosticEntry Unavailable(string entryName) => new()
    {
        Name = entryName,
        Kind = ModuleOptionDiagnosticValueKind.Unavailable
    };
}

internal sealed class PresenceOptionDiagnosticDefinition<TOptions>(
    string name,
    Func<TOptions, bool> getter) : IModuleOptionDiagnosticDefinition<TOptions>
{
    public ModuleOptionDiagnosticEntry Evaluate(TOptions options)
    {
        try
        {
            return new ModuleOptionDiagnosticEntry
            {
                Name = name,
                Kind = ModuleOptionDiagnosticValueKind.Presence,
                IsPresent = getter(options)
            };
        }
        catch
        {
            return new ModuleOptionDiagnosticEntry
            {
                Name = name,
                Kind = ModuleOptionDiagnosticValueKind.Unavailable
            };
        }
    }
}

internal sealed class CountOptionDiagnosticDefinition<TOptions>(
    string name,
    Func<TOptions, int> getter) : IModuleOptionDiagnosticDefinition<TOptions>
{
    public ModuleOptionDiagnosticEntry Evaluate(TOptions options)
    {
        try
        {
            var count = getter(options);
            return count < 0
                ? Unavailable()
                : new ModuleOptionDiagnosticEntry
                {
                    Name = name,
                    Kind = ModuleOptionDiagnosticValueKind.Count,
                    Count = count
                };
        }
        catch
        {
            return Unavailable();
        }
    }

    private ModuleOptionDiagnosticEntry Unavailable() => new()
    {
        Name = name,
        Kind = ModuleOptionDiagnosticValueKind.Unavailable
    };
}
