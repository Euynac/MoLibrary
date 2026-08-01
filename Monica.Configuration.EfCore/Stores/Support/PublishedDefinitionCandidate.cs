using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.Models;
using Monica.Configuration.Serialization;

namespace Monica.Configuration.EfCore.Stores.Support;

internal sealed record PublishedDefinitionCandidate
{
    public required string DefinitionIdentity { get; init; }

    public required string DefinitionKey { get; init; }

    public required string SectionPath { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public required string ClrTypeName { get; init; }

    public required string FromProject { get; init; }

    public string? Category { get; init; }

    public int SourceSchemaVersion { get; init; }

    public required string SchemaHash { get; init; }

    public required string ReloadBehavior { get; init; }

    public required string SchemaJson { get; init; }

    public static PublishedDefinitionCandidate FromPublication(
        ConfigurationDefinitionPublication publication,
        string definitionIdentity,
        ConfigurationReloadBehavior effectiveReloadBehavior)
    {
        var definition = publication.Definition;
        return new PublishedDefinitionCandidate
        {
            DefinitionIdentity = definitionIdentity,
            DefinitionKey = definition.DefinitionKey,
            SectionPath = definition.SectionPath,
            DisplayName = definition.DisplayName,
            Description = NullIfWhiteSpace(definition.Description),
            ClrTypeName = ConfigurationDefinitionSchemaCodec.ToCompactClrTypeName(definition.ClrTypeName),
            FromProject = definition.FromProject,
            Category = NullIfWhiteSpace(definition.Category),
            SourceSchemaVersion = Math.Max(definition.SchemaVersion, 1),
            SchemaHash = definition.SchemaHash,
            ReloadBehavior = effectiveReloadBehavior.ToString(),
            SchemaJson = ConfigurationDefinitionSchemaCodec.SerializeSchema(definition)
        };
    }

    public static PublishedDefinitionCandidate FromCurrent(
        ConfigurationDefinitionEntity current,
        ConfigurationReloadBehavior effectiveReloadBehavior)
    {
        return new PublishedDefinitionCandidate
        {
            DefinitionIdentity = current.DefinitionIdentity,
            DefinitionKey = current.DefinitionKey,
            SectionPath = current.SectionPath,
            DisplayName = current.DisplayName,
            Description = NullIfWhiteSpace(current.Description),
            ClrTypeName = current.ClrTypeName,
            FromProject = current.FromProject,
            Category = NullIfWhiteSpace(current.Category),
            SourceSchemaVersion = current.SchemaVersion,
            SchemaHash = current.SchemaHash,
            ReloadBehavior = effectiveReloadBehavior.ToString(),
            SchemaJson = current.SchemaJson
        };
    }

    public bool Matches(ConfigurationDefinitionEntity current)
    {
        return MatchesEnvelopeIgnoringReloadBehavior(current)
               && string.Equals(ReloadBehavior, current.ReloadBehavior, StringComparison.Ordinal);
    }

    public bool MatchesEnvelopeIgnoringReloadBehavior(ConfigurationDefinitionEntity current)
    {
        return string.Equals(DefinitionIdentity, current.DefinitionIdentity, StringComparison.Ordinal)
               && string.Equals(DefinitionKey, current.DefinitionKey, StringComparison.Ordinal)
               && HasSameSchema(current)
               && string.Equals(SectionPath, current.SectionPath, StringComparison.Ordinal)
               && string.Equals(DisplayName, current.DisplayName, StringComparison.Ordinal)
               && string.Equals(Description, NullIfWhiteSpace(current.Description), StringComparison.Ordinal)
               && string.Equals(ClrTypeName, current.ClrTypeName, StringComparison.Ordinal)
               && string.Equals(FromProject, current.FromProject, StringComparison.Ordinal)
               && string.Equals(Category, NullIfWhiteSpace(current.Category), StringComparison.Ordinal)
               && string.Equals(SchemaJson, current.SchemaJson, StringComparison.Ordinal);
    }

    public bool HasSameSchema(ConfigurationDefinitionEntity current)
    {
        return string.Equals(SchemaHash, current.SchemaHash, StringComparison.Ordinal);
    }

    public ConfigurationDefinitionEntity CreateEntity(int schemaVersion, int definitionRevision)
    {
        var entity = new ConfigurationDefinitionEntity
        {
            DefinitionIdentity = DefinitionIdentity,
            DefinitionKey = DefinitionKey,
            SchemaVersion = schemaVersion,
            DefinitionRevision = definitionRevision
        };
        ApplySnapshot(entity, schemaVersion);
        return entity;
    }

    public void ApplyTo(
        ConfigurationDefinitionEntity entity,
        int schemaVersion,
        int definitionRevision)
    {
        ApplySnapshot(entity, schemaVersion);
        entity.DefinitionRevision = definitionRevision;
    }

    public ConfigurationDefinitionPublishHistoryEntity CreateHistory(
        ConfigurationDefinitionEntity? current,
        ConfigurationDefinitionPublishChangeKind changeKind,
        ConfigurationPublisherIdentity publisher,
        int schemaVersion,
        int definitionRevision)
    {
        return new ConfigurationDefinitionPublishHistoryEntity
        {
            HistoryId = Guid.NewGuid().ToString("N"),
            DefinitionIdentity = DefinitionIdentity,
            DefinitionKey = DefinitionKey,
            SectionPath = SectionPath,
            DisplayName = DisplayName,
            Description = Description,
            FromProject = FromProject,
            Category = Category,
            ChangeKind = changeKind.ToString(),
            DefinitionRevision = definitionRevision,
            PreviousSchemaVersion = current?.SchemaVersion,
            NewSchemaVersion = schemaVersion,
            PreviousSchemaHash = current?.SchemaHash,
            NewSchemaHash = SchemaHash,
            PreviousSchemaJson = current?.SchemaJson,
            NewSchemaJson = SchemaJson,
            ChangeSummaryJson = CreateChangeSummaryJson(current, changeKind),
            PublisherId = publisher.InstanceId,
            PublisherName = publisher.Name,
            PublisherVersion = publisher.Version,
            PublishedTime = DateTime.UtcNow
        };
    }

    private void ApplySnapshot(ConfigurationDefinitionEntity entity, int schemaVersion)
    {
        entity.DefinitionIdentity = DefinitionIdentity;
        entity.SectionPath = SectionPath;
        entity.DisplayName = DisplayName;
        entity.Description = Description;
        entity.ClrTypeName = ClrTypeName;
        entity.FromProject = FromProject;
        entity.Category = NullIfWhiteSpace(Category);
        entity.SchemaVersion = schemaVersion;
        entity.SchemaHash = SchemaHash;
        entity.ReloadBehavior = ReloadBehavior;
        entity.SchemaJson = SchemaJson;
    }

    public int ResolveNewSchemaVersion(
        ConfigurationDefinitionEntity? current,
        ConfigurationDefinitionPublishChangeKind changeKind)
    {
        if (current is null)
        {
            return SourceSchemaVersion;
        }

        var currentVersion = Math.Max(current.SchemaVersion, 1);
        return changeKind == ConfigurationDefinitionPublishChangeKind.SchemaChanged
            ? checked(Math.Max(currentVersion, SourceSchemaVersion) + 1)
            : currentVersion;
    }

    private string CreateChangeSummaryJson(
        ConfigurationDefinitionEntity? current,
        ConfigurationDefinitionPublishChangeKind changeKind)
    {
        var changes = new List<SchemaPublishChangeDto>();
        if (current is null)
        {
            changes.Add(new SchemaPublishChangeDto("Definition", null, DefinitionKey));
        }
        else
        {
            AddChange(changes, nameof(SectionPath), current.SectionPath, SectionPath);
            AddChange(changes, nameof(DisplayName), current.DisplayName, DisplayName);
            AddChange(changes, nameof(Description), NullIfWhiteSpace(current.Description), Description);
            AddChange(changes, nameof(ClrTypeName), current.ClrTypeName, ClrTypeName);
            AddChange(changes, nameof(FromProject), current.FromProject, FromProject);
            AddChange(changes, nameof(Category), NullIfWhiteSpace(current.Category), Category);
            AddChange(changes, nameof(ReloadBehavior), current.ReloadBehavior, ReloadBehavior);
            AddChange(changes, nameof(SchemaHash), current.SchemaHash, SchemaHash);
            AddChange(
                changes,
                $"{nameof(SchemaJson)}Fingerprint",
                Fingerprint(current.SchemaJson),
                Fingerprint(SchemaJson));
        }

        return JsonSerializer.Serialize(
            new SchemaPublishChangeSummaryDto(changeKind.ToString(), changes),
            ConfigurationPersistedJsonOptions.CompactSchema);
    }

    private static void AddChange(
        List<SchemaPublishChangeDto> changes,
        string field,
        string? previousValue,
        string? newValue)
    {
        if (!string.Equals(previousValue, newValue, StringComparison.Ordinal))
        {
            changes.Add(new SchemaPublishChangeDto(field, previousValue, newValue));
        }
    }

    private static string Fingerprint(string? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record SchemaPublishChangeSummaryDto(
        string ChangeKind,
        IReadOnlyList<SchemaPublishChangeDto> Changes);

    private sealed record SchemaPublishChangeDto(
        string Field,
        string? PreviousValue,
        string? NewValue);
}
