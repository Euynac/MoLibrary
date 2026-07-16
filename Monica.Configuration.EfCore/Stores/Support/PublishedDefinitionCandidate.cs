using System.Reflection;
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

    public static PublishedDefinitionCandidate FromDefinition(ConfigurationDefinition definition)
    {
        return new PublishedDefinitionCandidate
        {
            DefinitionIdentity = ConfigurationDefinitionIdentity.Compute(definition.DefinitionKey),
            DefinitionKey = definition.DefinitionKey,
            SectionPath = definition.SectionPath,
            DisplayName = definition.DisplayName,
            Description = NullIfWhiteSpace(definition.Description),
            ClrTypeName = ConfigurationDefinitionSchemaCodec.ToCompactClrTypeName(definition.ClrTypeName),
            FromProject = definition.FromProject,
            Category = NullIfWhiteSpace(definition.Category),
            SourceSchemaVersion = Math.Max(definition.SchemaVersion, 1),
            SchemaHash = definition.SchemaHash,
            ReloadBehavior = definition.ReloadBehavior.ToString(),
            SchemaJson = ConfigurationDefinitionSchemaCodec.SerializeSchema(definition)
        };
    }

    public bool Matches(ConfigurationDefinitionEntity current)
    {
        return HasSameSchema(current)
               && current.SchemaVersion >= SourceSchemaVersion
               && string.Equals(SectionPath, current.SectionPath, StringComparison.Ordinal)
               && string.Equals(DisplayName, current.DisplayName, StringComparison.Ordinal)
               && string.Equals(Description, NullIfWhiteSpace(current.Description), StringComparison.Ordinal)
               && string.Equals(ClrTypeName, current.ClrTypeName, StringComparison.Ordinal)
               && string.Equals(FromProject, current.FromProject, StringComparison.Ordinal)
               && string.Equals(Category, NullIfWhiteSpace(current.Category), StringComparison.Ordinal)
               && string.Equals(ReloadBehavior, current.ReloadBehavior, StringComparison.Ordinal)
               && string.Equals(SchemaJson, current.SchemaJson, StringComparison.Ordinal);
    }

    public bool HasSameSchema(ConfigurationDefinitionEntity current)
    {
        return string.Equals(SchemaHash, current.SchemaHash, StringComparison.Ordinal);
    }

    public ConfigurationDefinitionEntity CreateEntity()
    {
        var entity = new ConfigurationDefinitionEntity
        {
            DefinitionIdentity = DefinitionIdentity,
            DefinitionKey = DefinitionKey,
            SchemaVersion = SourceSchemaVersion,
            PublishRevision = 1
        };
        ApplySnapshot(entity, SourceSchemaVersion);
        return entity;
    }

    public void ApplyTo(ConfigurationDefinitionEntity entity, int schemaVersion)
    {
        ApplySnapshot(entity, schemaVersion);
        entity.PublishRevision = Math.Max(entity.PublishRevision, 0) + 1;
    }

    public ConfigurationDefinitionPublishHistoryEntity CreateHistory(
        ConfigurationDefinitionEntity? current,
        ConfigurationDefinitionPublishChangeKind changeKind)
    {
        var publisher = PublishActor.Capture();
        var newSchemaVersion = ResolveNewSchemaVersion(current, changeKind);
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
            PreviousSchemaVersion = current?.SchemaVersion,
            NewSchemaVersion = newSchemaVersion,
            PreviousSchemaHash = current?.SchemaHash,
            NewSchemaHash = SchemaHash,
            PreviousSchemaJson = current?.SchemaJson,
            NewSchemaJson = SchemaJson,
            ChangeSummaryJson = CreateChangeSummaryJson(current, changeKind),
            PublisherId = publisher.PublisherId,
            PublisherName = publisher.PublisherName,
            PublisherVersion = publisher.PublisherVersion,
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

    private int ResolveNewSchemaVersion(
        ConfigurationDefinitionEntity? current,
        ConfigurationDefinitionPublishChangeKind changeKind)
    {
        if (current is null)
        {
            return SourceSchemaVersion;
        }

        var currentVersion = Math.Max(Math.Max(current.SchemaVersion, SourceSchemaVersion), 1);
        return changeKind == ConfigurationDefinitionPublishChangeKind.SchemaChanged
            ? checked(currentVersion + 1)
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

    private sealed record PublishActor(
        string PublisherId,
        string PublisherName,
        string? PublisherVersion)
    {
        internal static PublishActor Capture()
        {
            var publisherName = FirstNonEmpty(
                Environment.GetEnvironmentVariable("MONICA_CONFIGURATION_INSTANCE_NAME"),
                Environment.GetEnvironmentVariable("HOSTNAME"),
                Environment.MachineName);
            var publisherId = FirstNonEmpty(
                Environment.GetEnvironmentVariable("MONICA_CONFIGURATION_INSTANCE_ID"),
                $"{publisherName}:{Environment.ProcessId}");
            return new PublishActor(publisherId, publisherName, GetEntryAssemblyVersion());
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "unknown";
        }

        private static string? GetEntryAssemblyVersion()
        {
            var assembly = Assembly.GetEntryAssembly();
            return assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly?.GetName().Version?.ToString();
        }
    }

    private sealed record SchemaPublishChangeSummaryDto(
        string ChangeKind,
        IReadOnlyList<SchemaPublishChangeDto> Changes);

    private sealed record SchemaPublishChangeDto(
        string Field,
        string? PreviousValue,
        string? NewValue);
}
