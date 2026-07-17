using System.Text.Json.Serialization;

namespace Monica.Configuration.Models;

/// <summary>
/// Represents the raw persisted envelope of one published configuration definition.
/// </summary>
/// <remarks>
/// This store-facing model intentionally carries raw schema JSON so a metadata store can preserve trustworthy
/// publisher fields even when schema materialization fails. Facades must map it to display-safe models and must
/// never expose <see cref="SchemaJson"/> directly.
/// </remarks>
public sealed record ConfigurationPublishedDefinitionRecord
{
    /// <summary>
    /// Gets the metadata store that supplied this record.
    /// </summary>
    public required string StoreKey { get; init; }

    /// <summary>
    /// Gets the stable definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the Microsoft configuration section path.
    /// </summary>
    public required string SectionPath { get; init; }

    /// <summary>
    /// Gets the operator-facing display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the optional developer-facing description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the persisted root CLR type identity.
    /// </summary>
    public required string ClrTypeName { get; init; }

    /// <summary>
    /// Gets the project that published the definition.
    /// </summary>
    public required string FromProject { get; init; }

    /// <summary>
    /// Gets the optional operator-facing category.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the persisted schema version.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets the persisted schema hash.
    /// </summary>
    public required string SchemaHash { get; init; }

    /// <summary>
    /// Gets the raw persisted reload behavior name.
    /// </summary>
    public required string ReloadBehavior { get; init; }

    /// <summary>
    /// Gets the raw persisted schema JSON.
    /// </summary>
    [JsonIgnore]
    public required string SchemaJson { get; init; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{nameof(ConfigurationPublishedDefinitionRecord)} {{ StoreKey = {StoreKey}, "
               + $"DefinitionKey = {DefinitionKey}, FromProject = {FromProject}, SchemaVersion = {SchemaVersion}, "
               + $"SchemaHash = {SchemaHash}, SchemaJson = <redacted> }}";
    }
}
