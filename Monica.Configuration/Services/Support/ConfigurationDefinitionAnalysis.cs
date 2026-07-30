using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Holds one complete, immutable result of scanning the host's managed configuration types.
/// </summary>
internal sealed class ConfigurationDefinitionAnalysis
{
    public static ConfigurationDefinitionAnalysis Empty { get; } = new([], []);

    private ConfigurationDefinitionAnalysis(
        IReadOnlyList<ConfigurationDefinitionRegistration> registrations,
        IReadOnlyList<ConfigurationSectionPathConflict> sectionPathConflicts)
    {
        Registrations = registrations;
        SectionPathConflicts = sectionPathConflicts;
    }

    public IReadOnlyList<ConfigurationDefinitionRegistration> Registrations { get; }

    public IReadOnlyList<ConfigurationSectionPathConflict> SectionPathConflicts { get; }

    /// <summary>
    /// Scans every selected options type without publishing partial definitions when a scan fails.
    /// </summary>
    public static ConfigurationDefinitionAnalysis Create(
        ConfigurationDefinitionScanner scanner,
        IEnumerable<Type> optionsTypes)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(optionsTypes);

        var registrations = optionsTypes
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .Select(type => new ConfigurationDefinitionRegistration(type, scanner.Scan(type)))
            .OrderBy(static registration => registration.Definition.DefinitionKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static registration => registration.OptionsType.FullName, StringComparer.Ordinal)
            .ToArray();

        return new ConfigurationDefinitionAnalysis(registrations, []);
    }

    public ConfigurationDefinitionAnalysis WithSectionPathConflicts(
        IReadOnlyList<ConfigurationSectionPathConflict> conflicts)
    {
        ArgumentNullException.ThrowIfNull(conflicts);
        return new ConfigurationDefinitionAnalysis(Registrations, conflicts.ToArray());
    }
}

/// <summary>
/// Couples a scanned definition to the CLR options type that must be bound after analysis completes.
/// </summary>
internal sealed record ConfigurationDefinitionRegistration(
    Type OptionsType,
    ConfigurationDefinition Definition);

/// <summary>
/// Identifies two definitions that intentionally share one Microsoft configuration section.
/// </summary>
internal sealed record ConfigurationSectionPathConflict(
    ConfigurationDefinition Existing,
    ConfigurationDefinition Duplicate);
