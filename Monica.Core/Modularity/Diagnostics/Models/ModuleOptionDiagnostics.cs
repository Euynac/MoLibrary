using System.Collections.Immutable;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Contains a detached catalog of every public option property with bounded projected values.
/// </summary>
public sealed record ModuleOptionDiagnostics
{
    /// <summary>Gets the module whose options were projected.</summary>
    public required ModuleKey ModuleKey { get; init; }

    /// <summary>Gets the concrete option type's clean full name.</summary>
    public required string OptionTypeName { get; init; }

    /// <summary>Gets the requested named profile, or <see langword="null"/> for an explicit default request.</summary>
    public string? RequestedProfileName { get; init; }

    /// <summary>Gets the effective named profile, or <see langword="null"/> when default options were projected.</summary>
    public string? ProfileName { get; init; }

    /// <summary>Gets how the requested profile was resolved.</summary>
    public ModuleOptionProfileResolution ProfileResolution { get; init; }

    /// <summary>Gets the disclosure mode used for this projection.</summary>
    public ModuleOptionDiagnosticsExposureMode ExposureMode { get; init; }

    /// <summary>Gets whether the requested option instance had been finalized when it was projected.</summary>
    public bool IsFinalized { get; init; }

    /// <summary>
    /// Gets every public, non-indexed configuration property in declaration order. Nested values and collection
    /// items are bounded; properties whose values cannot be read safely remain represented as metadata-only entries.
    /// </summary>
    public ImmutableArray<ModuleOptionDiagnosticEntry> Entries { get; init; } = [];

    /// <summary>Gets the number of sensitive entries protected or revealed by this projection.</summary>
    public int SensitiveEntryCount { get; init; }

    /// <summary>
    /// Gets whether a nested-property or collection-item bound was reached, or collection reading was interrupted.
    /// </summary>
    public bool IsTruncated { get; init; }

    /// <summary>Gets whether at least one bounded sensitive scalar is present in clear text.</summary>
    public bool ContainsRevealedSensitiveValues { get; init; }
}

/// <summary>Describes one detached option value or configuration group.</summary>
public sealed record ModuleOptionDiagnosticEntry
{
    /// <summary>Gets the developer-facing property or collection-item name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the stable path relative to the option root.</summary>
    public required string Path { get; init; }

    /// <summary>Gets the declared or runtime value type's clean full name, including readable generic arguments.</summary>
    public required string TypeName { get; init; }

    /// <summary>Gets the projection kind.</summary>
    public ModuleOptionDiagnosticValueKind Kind { get; init; }

    /// <summary>Gets the bounded invariant representation for a scalar value.</summary>
    public string? Value { get; init; }

    /// <summary>Gets whether a redacted value is configured.</summary>
    public bool? IsPresent { get; init; }

    /// <summary>Gets the collection's total count when it can be obtained without unsafe enumeration.</summary>
    public int? Count { get; init; }

    /// <summary>Gets whether the value is explicitly <see langword="null"/>.</summary>
    public bool IsNull { get; init; }

    /// <summary>Gets whether this value or group was shortened to a diagnostics bound.</summary>
    public bool IsTruncated { get; init; }

    /// <summary>Gets whether the value is classified as sensitive.</summary>
    public bool IsSensitive { get; init; }

    /// <summary>Gets bounded child properties or collection items.</summary>
    public ImmutableArray<ModuleOptionDiagnosticEntry> Children { get; init; } = [];
}

/// <summary>Defines the detached representations permitted across the diagnostics boundary.</summary>
public enum ModuleOptionDiagnosticValueKind
{
    /// <summary>A bounded scalar value.</summary>
    Value,

    /// <summary>Only the presence of a sensitive value is disclosed.</summary>
    Presence,

    /// <summary>Only a collection count is available.</summary>
    Count,

    /// <summary>A bounded object group with projected children.</summary>
    Object,

    /// <summary>A bounded collection group with projected children.</summary>
    Collection,

    /// <summary>A property getter failed without exposing exception details.</summary>
    Unavailable,

    /// <summary>
    /// The property remains cataloged by name and type, but reading or traversing its value is unsafe or unsupported.
    /// </summary>
    Unsupported
}

/// <summary>Controls whether sensitive module configuration values are redacted.</summary>
public enum ModuleOptionDiagnosticsExposureMode
{
    /// <summary>
    /// Automatically exposes ordinary bounded configuration while replacing sensitive values with presence state.
    /// </summary>
    Redacted,

    /// <summary>
    /// Reveals bounded sensitive scalar values in the Development environment for dedicated local debugging. Every
    /// public property remains represented, computed and runtime-shaped values stay metadata-only, and portable
    /// exports still omit option diagnostics.
    /// </summary>
    RevealSensitive
}

/// <summary>Describes how a module option profile request resolved.</summary>
public enum ModuleOptionProfileResolution
{
    /// <summary>The module's default options were requested directly.</summary>
    Default,

    /// <summary>The requested named profile was found.</summary>
    Named,

    /// <summary>The requested named profile did not exist and an explicitly permitted default fallback was used.</summary>
    DefaultFallback
}
