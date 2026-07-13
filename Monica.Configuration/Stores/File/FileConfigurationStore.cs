using System.Text.Json;
using IoDirectory = System.IO.Directory;
using IoFile = System.IO.File;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Serialization;
using Monica.Configuration.Utils;
using Microsoft.Extensions.Options;

namespace Monica.Configuration.Stores.File;

/// <summary>
/// File-backed store bundle for monolith and local Monica.Configuration deployments.
/// </summary>
public sealed class FileConfigurationStore(IOptions<ConfigurationFileStoreOptions> options)
    : IConfigurationEffectiveValueStore, IConfigurationHistoryStore, IConfigurationMetadataStore, IConfigurationUnifiedVersionStore
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        Encoder = ConfigurationPersistedJsonOptions.ReadableValue.Encoder,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions JSON_LINE_OPTIONS = ConfigurationPersistedJsonOptions.CompactValue;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConfigurationFileStoreOptions _options = options.Value;

    /// <inheritdoc />
    public ConfigurationStoreDescriptor Descriptor { get; } = new()
    {
        StoreKey = "file:default",
        DisplayName = "File",
        Kind = ConfigurationStoreKind.File,
        SupportsEffectiveValues = true,
        SupportsHistory = true,
        SupportsMetadata = true
    };

    /// <inheritdoc />
    public async Task<ConfigurationEffectiveValueDocument> EnsureCreatedAsync(
        ConfigurationDefinition definition,
        string seedJson,
        CancellationToken cancellationToken)
    {
        var documents = await EnsureCreatedAsync(
            [new ConfigurationEffectiveValueSeed { Definition = definition, SeedJson = seedJson }],
            cancellationToken);
        return documents[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationEffectiveValueDocument>> EnsureCreatedAsync(
        IReadOnlyList<ConfigurationEffectiveValueSeed> seeds,
        CancellationToken cancellationToken)
    {
        if (seeds.Count == 0)
        {
            return [];
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            var documentsByKey = new Dictionary<string, ConfigurationEffectiveValueDocument>(StringComparer.OrdinalIgnoreCase);
            foreach (var seed in seeds)
            {
                if (documentsByKey.ContainsKey(seed.Definition.DefinitionKey))
                {
                    continue;
                }

                var path = GetEffectiveValuePath(seed.Definition.DefinitionKey);
                if (IoFile.Exists(path))
                {
                    documentsByKey[seed.Definition.DefinitionKey] =
                        await ReadDocumentAsync(seed.Definition.DefinitionKey, cancellationToken)
                        ?? throw new InvalidOperationException($"Configuration document '{path}' could not be read.");
                    continue;
                }

                var document = new ConfigurationEffectiveValueDocument
                {
                    DefinitionKey = seed.Definition.DefinitionKey,
                    Json = FormatJson(seed.SeedJson),
                    Version = 1,
                    SchemaVersion = seed.Definition.SchemaVersion,
                    LastModifiedTime = DateTimeOffset.UtcNow
                };

                await WriteDocumentAsync(document, cancellationToken);
                documentsByKey[seed.Definition.DefinitionKey] = document;
            }

            return seeds
                .Select(seed => documentsByKey[seed.Definition.DefinitionKey])
                .ToArray();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ConfigurationEffectiveValueDocument?> GetAsync(string definitionKey, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await ReadDocumentAsync(definitionKey, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ConfigurationEffectiveValueDocument> SaveAsync(
        ConfigurationEffectiveValueSaveRequest request,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            var existing = await ReadDocumentAsync(request.Definition.DefinitionKey, cancellationToken);
            var currentVersion = existing?.Version ?? 0;
            if (request.ExpectedVersion is not null && currentVersion != request.ExpectedVersion)
            {
                throw new ConfigurationConcurrencyConflictException(
                    $"Expected version {request.ExpectedVersion} for '{request.Definition.DefinitionKey}', but current version is {currentVersion}.");
            }

            var now = DateTimeOffset.UtcNow;
            var document = new ConfigurationEffectiveValueDocument
            {
                DefinitionKey = request.Definition.DefinitionKey,
                Json = FormatJson(request.Json),
                Version = (existing?.Version ?? 0) + 1,
                SchemaVersion = request.Definition.SchemaVersion,
                LastModifiedTime = now,
                LastModifierId = request.Context.ModifierId,
                LastModifierName = request.Context.ModifierName
            };

            await WriteDocumentAsync(document, cancellationToken);
            return document;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task AppendHistoryAsync(ConfigurationValueHistory history, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            var line = JsonSerializer.Serialize(HistoryDto.FromHistory(history), JSON_LINE_OPTIONS);
            await IoFile.AppendAllTextAsync(GetHistoryPath(), line + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationValueHistory>> QueryHistoryAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        LogicalPath? logicalPath,
        string? mutationGroupId,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var path = GetHistoryPath();
            if (!IoFile.Exists(path))
            {
                return [];
            }

            var canonicalPath = logicalPath?.ToCanonicalString();
            var result = new List<ConfigurationValueHistory>();
            foreach (var line in await IoFile.ReadAllLinesAsync(path, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var history = JsonSerializer.Deserialize<HistoryDto>(line)?.ToHistory();
                if (history is null)
                {
                    continue;
                }

                if (from is not null && history.ModifiedTime < from
                    || to is not null && history.ModifiedTime > to
                    || !string.IsNullOrWhiteSpace(definitionKey) && !string.Equals(history.DefinitionKey, definitionKey, StringComparison.OrdinalIgnoreCase)
                    || canonicalPath is not null && history.LogicalPath.ToCanonicalString() != canonicalPath
                    || !string.IsNullOrWhiteSpace(mutationGroupId) && !string.Equals(history.MutationGroupId, mutationGroupId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(history);
            }

            return SortHistory(result);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ConfigurationValueHistory?> GetHistoryByIdAsync(string historyId, CancellationToken cancellationToken)
    {
        var rows = await QueryHistoryAsync(null, null, null, null, null, cancellationToken);
        return rows.FirstOrDefault(row => string.Equals(row.HistoryId, historyId, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task UpsertGroupAsync(ConfigurationMutationGroup group, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            var groups = await ReadGroupsAsync(cancellationToken);
            var index = groups.FindIndex(candidate => string.Equals(candidate.GroupId, group.GroupId, StringComparison.OrdinalIgnoreCase));
            var dto = GroupDto.FromGroup(group);
            if (index >= 0)
            {
                groups[index] = dto;
            }
            else
            {
                groups.Add(dto);
            }

            await IoFile.WriteAllTextAsync(GetGroupsPath(), JsonSerializer.Serialize(groups, JSON_OPTIONS), cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationMutationGroup>> ListGroupsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return (await ReadGroupsAsync(cancellationToken))
                .Select(group => group.ToGroup())
                .Where(group =>
                    (from is null || group.CreatedTime >= from)
                    && (to is null || group.CreatedTime <= to)
                    && (string.IsNullOrWhiteSpace(definitionKey) || group.DefinitionKeys.Contains(definitionKey, StringComparer.OrdinalIgnoreCase)))
                .OrderByDescending(group => group.CreatedTime)
                .ToArray();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ConfigurationMutationGroup?> GetGroupAsync(string groupId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return (await ReadGroupsAsync(cancellationToken))
                .Select(group => group.ToGroup())
                .FirstOrDefault(group => string.Equals(group.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ConfigurationUnifiedVersionSnapshot> AppendVersionAsync(
        ConfigurationUnifiedVersionCreateRequest request,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            var summaries = await ReadUnifiedVersionIndexAsync(cancellationToken);
            var version = summaries.Count == 0 ? 1 : summaries.Max(static summary => summary.Version) + 1;
            var summary = new ConfigurationUnifiedVersionSummary
            {
                Version = version,
                MutationGroupId = request.MutationGroupId,
                TriggerDefinitionKeys = NormalizeKeys(request.TriggerDefinitionKeys),
                DefinitionKeys = NormalizeKeys(request.Definitions.Select(static definition => definition.DefinitionKey)),
                DefinitionCount = request.Definitions.Count,
                CreatedTime = request.CreatedTime,
                ModifierId = request.ModifierId,
                ModifierName = request.ModifierName,
                Reason = request.Reason
            };
            var snapshot = new ConfigurationUnifiedVersionSnapshot
            {
                Summary = summary,
                Definitions = request.Definitions
            };

            await IoFile.WriteAllTextAsync(
                GetUnifiedVersionSnapshotPath(version),
                JsonSerializer.Serialize(snapshot, JSON_OPTIONS),
                cancellationToken);
            summaries.Add(summary);
            summaries = summaries
                .OrderByDescending(static item => item.Version)
                .ToList();
            await IoFile.WriteAllTextAsync(
                GetUnifiedVersionIndexPath(),
                JsonSerializer.Serialize(summaries, JSON_OPTIONS),
                cancellationToken);
            return snapshot;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationUnifiedVersionSummary>> ListVersionsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        int limit,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var normalizedLimit = Math.Clamp(limit, 1, 500);
            return (await ReadUnifiedVersionIndexAsync(cancellationToken))
                .Where(summary =>
                    (from is null || summary.CreatedTime >= from)
                    && (to is null || summary.CreatedTime <= to)
                    && (string.IsNullOrWhiteSpace(definitionKey)
                        || summary.DefinitionKeys.Contains(definitionKey, StringComparer.OrdinalIgnoreCase)))
                .OrderByDescending(static summary => summary.Version)
                .Take(normalizedLimit)
                .ToArray();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ConfigurationUnifiedVersionSnapshot?> GetVersionAsync(
        long version,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var path = GetUnifiedVersionSnapshotPath(version);
            return IoFile.Exists(path)
                ? JsonSerializer.Deserialize<ConfigurationUnifiedVersionSnapshot>(
                    await IoFile.ReadAllTextAsync(path, cancellationToken),
                    JSON_OPTIONS)
                : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ConfigurationUnifiedVersionSnapshot?> GetVersionByMutationGroupAsync(
        string mutationGroupId,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var summary = (await ReadUnifiedVersionIndexAsync(cancellationToken))
                .FirstOrDefault(candidate => string.Equals(candidate.MutationGroupId, mutationGroupId, StringComparison.OrdinalIgnoreCase));
            if (summary is null)
            {
                return null;
            }

            var path = GetUnifiedVersionSnapshotPath(summary.Version);
            return IoFile.Exists(path)
                ? JsonSerializer.Deserialize<ConfigurationUnifiedVersionSnapshot>(
                    await IoFile.ReadAllTextAsync(path, cancellationToken),
                    JSON_OPTIONS)
                : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync(IReadOnlyList<ConfigurationDefinition> definitions, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            foreach (var definition in definitions)
            {
                var path = GetDefinitionPath(definition.DefinitionKey);
                var existing = IoFile.Exists(path) ? ReadPublishedDefinitionDto(path) : null;
                var published = PublishedDefinitionDto.FromDefinition(definition, existing);
                if (existing is not null && published.Matches(existing))
                {
                    continue;
                }

                await IoFile.WriteAllTextAsync(path, JsonSerializer.Serialize(published, JSON_OPTIONS), cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ConfigurationDefinitionPublishHistory>> ListDefinitionPublishHistoriesAsync(
        string definitionKey,
        int limit,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ConfigurationDefinitionPublishHistory>>([]);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationDefinition>> ListPublishedDefinitionsAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            return IoDirectory.EnumerateFiles(GetDefinitionsDirectory(), "*.json")
                .Select(path => ReadPublishedDefinition(path))
                .OfType<ConfigurationDefinition>()
                .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ConfigurationDefinition?> GetPublishedDefinitionAsync(string definitionKey, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            var path = GetDefinitionPath(definitionKey);
            return IoFile.Exists(path) ? ReadPublishedDefinition(path) : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<ConfigurationEffectiveValueDocument?> ReadDocumentAsync(
        string definitionKey,
        CancellationToken cancellationToken)
    {
        var path = GetEffectiveValuePath(definitionKey);
        if (!IoFile.Exists(path))
        {
            return null;
        }

        var metadata = await ReadMetadataAsync(definitionKey, cancellationToken);
        var json = await IoFile.ReadAllTextAsync(path, cancellationToken);
        var lastWrite = IoFile.GetLastWriteTimeUtc(path);
        return new ConfigurationEffectiveValueDocument
        {
            DefinitionKey = definitionKey,
            Json = FormatJson(json),
            Version = metadata?.Version ?? 1,
            SchemaVersion = metadata?.SchemaVersion ?? 1,
            LastModifiedTime = metadata?.LastModifiedTime ?? new DateTimeOffset(lastWrite, TimeSpan.Zero),
            LastModifierId = metadata?.LastModifierId,
            LastModifierName = metadata?.LastModifierName
        };
    }

    private async Task WriteDocumentAsync(ConfigurationEffectiveValueDocument document, CancellationToken cancellationToken)
    {
        await IoFile.WriteAllTextAsync(GetEffectiveValuePath(document.DefinitionKey), document.Json, cancellationToken);
        var metadata = new DocumentMetadataDto
        {
            Version = document.Version,
            SchemaVersion = document.SchemaVersion,
            LastModifiedTime = document.LastModifiedTime,
            LastModifierId = document.LastModifierId,
            LastModifierName = document.LastModifierName
        };
        await IoFile.WriteAllTextAsync(GetMetadataPath(document.DefinitionKey), JsonSerializer.Serialize(metadata, JSON_OPTIONS), cancellationToken);
    }

    private async Task<DocumentMetadataDto?> ReadMetadataAsync(string definitionKey, CancellationToken cancellationToken)
    {
        var path = GetMetadataPath(definitionKey);
        if (!IoFile.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<DocumentMetadataDto>(await IoFile.ReadAllTextAsync(path, cancellationToken));
    }

    private async Task<List<GroupDto>> ReadGroupsAsync(CancellationToken cancellationToken)
    {
        var path = GetGroupsPath();
        if (!IoFile.Exists(path))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<GroupDto>>(await IoFile.ReadAllTextAsync(path, cancellationToken)) ?? [];
    }

    private async Task<List<ConfigurationUnifiedVersionSummary>> ReadUnifiedVersionIndexAsync(
        CancellationToken cancellationToken)
    {
        var path = GetUnifiedVersionIndexPath();
        if (!IoFile.Exists(path))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<ConfigurationUnifiedVersionSummary>>(
            await IoFile.ReadAllTextAsync(path, cancellationToken),
            JSON_OPTIONS) ?? [];
    }

    private void EnsureDirectories()
    {
        IoDirectory.CreateDirectory(GetEffectiveDirectory());
        IoDirectory.CreateDirectory(GetEffectiveMetadataDirectory());
        IoDirectory.CreateDirectory(GetHistoryDirectory());
        IoDirectory.CreateDirectory(GetUnifiedVersionDirectory());
        IoDirectory.CreateDirectory(GetDefinitionsDirectory());
    }

    private string GetEffectiveDirectory()
    {
        return Path.Combine(_options.RootDirectory, "effective");
    }

    private string GetEffectiveMetadataDirectory()
    {
        return Path.Combine(GetEffectiveDirectory(), ".metadata");
    }

    private string GetHistoryDirectory()
    {
        return Path.Combine(_options.RootDirectory, "history");
    }

    private string GetDefinitionsDirectory()
    {
        return Path.Combine(_options.RootDirectory, "metadata", "definitions");
    }

    private string GetEffectiveValuePath(string definitionKey)
    {
        return Path.Combine(GetEffectiveDirectory(), $"{GetSafeFileName(definitionKey)}.json");
    }

    private string GetMetadataPath(string definitionKey)
    {
        return Path.Combine(GetEffectiveMetadataDirectory(), $"{GetSafeFileName(definitionKey)}.metadata.json");
    }

    private string GetDefinitionPath(string definitionKey)
    {
        return Path.Combine(GetDefinitionsDirectory(), $"{GetSafeFileName(definitionKey)}.json");
    }

    private string GetHistoryPath()
    {
        return Path.Combine(GetHistoryDirectory(), "history.jsonl");
    }

    private string GetGroupsPath()
    {
        return Path.Combine(GetHistoryDirectory(), "groups.json");
    }

    private string GetUnifiedVersionDirectory()
    {
        return Path.Combine(GetHistoryDirectory(), "unified-versions");
    }

    private string GetUnifiedVersionIndexPath()
    {
        return Path.Combine(GetUnifiedVersionDirectory(), "index.json");
    }

    private string GetUnifiedVersionSnapshotPath(long version)
    {
        return Path.Combine(GetUnifiedVersionDirectory(), $"v{version}.json");
    }

    private static string GetSafeFileName(string definitionKey)
    {
        if (string.IsNullOrWhiteSpace(definitionKey)
            || definitionKey.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || definitionKey.Contains(Path.DirectorySeparatorChar)
            || definitionKey.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ConfigurationValidationFailedException(
                $"Configuration definition key '{definitionKey}' cannot be used as a file-backed store name.");
        }

        return definitionKey;
    }

    private static string FormatJson(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return JsonSerializer.Serialize(document.RootElement, JSON_OPTIONS);
    }

    private static IReadOnlyList<string> NormalizeKeys(IEnumerable<string> keys)
    {
        return keys
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ConfigurationDefinition? ReadPublishedDefinition(string path)
    {
        return ReadPublishedDefinitionDto(path)?.ToDefinition();
    }

    private static PublishedDefinitionDto? ReadPublishedDefinitionDto(string path)
    {
        return JsonSerializer.Deserialize<PublishedDefinitionDto>(IoFile.ReadAllText(path), JSON_OPTIONS);
    }

    private static IReadOnlyList<ConfigurationValueHistory> SortHistory(IEnumerable<ConfigurationValueHistory> histories)
    {
        return histories
            .OrderByDescending(history => history.ModifiedTime)
            .ThenByDescending(history => history.Version)
            .ToArray();
    }

    private sealed record DocumentMetadataDto
    {
        public long Version { get; init; }

        public int SchemaVersion { get; init; }

        public DateTimeOffset LastModifiedTime { get; init; }

        public string? LastModifierId { get; init; }

        public string? LastModifierName { get; init; }
    }

    private sealed record PublishedDefinitionDto
    {
        public string DefinitionKey { get; init; } = "";

        public string SectionPath { get; init; } = "";

        public string DisplayName { get; init; } = "";

        public string? Description { get; init; }

        public string ClrTypeName { get; init; } = "";

        public string FromProject { get; init; } = "";

        public string? Category { get; init; }

        public int SchemaVersion { get; init; }

        public string SchemaHash { get; init; } = "";

        public string ReloadBehavior { get; init; } = "";

        public string SchemaJson { get; init; } = "";

        public static PublishedDefinitionDto FromDefinition(
            ConfigurationDefinition definition,
            PublishedDefinitionDto? existing)
        {
            return new PublishedDefinitionDto
            {
                DefinitionKey = definition.DefinitionKey,
                SectionPath = definition.SectionPath,
                DisplayName = definition.DisplayName,
                Description = NullIfWhiteSpace(definition.Description),
                ClrTypeName = ConfigurationDefinitionSchemaCodec.ToCompactClrTypeName(definition.ClrTypeName),
                FromProject = definition.FromProject,
                Category = NullIfWhiteSpace(definition.Category),
                SchemaVersion = ResolvePublishedSchemaVersion(definition, existing),
                SchemaHash = definition.SchemaHash,
                ReloadBehavior = definition.ReloadBehavior.ToString(),
                SchemaJson = ConfigurationDefinitionSchemaCodec.SerializeSchema(definition)
            };
        }

        public bool Matches(PublishedDefinitionDto existing)
        {
            return string.Equals(DefinitionKey, existing.DefinitionKey, StringComparison.Ordinal)
                   && string.Equals(SectionPath, existing.SectionPath, StringComparison.Ordinal)
                   && string.Equals(DisplayName, existing.DisplayName, StringComparison.Ordinal)
                   && string.Equals(Description, NullIfWhiteSpace(existing.Description), StringComparison.Ordinal)
                   && string.Equals(ClrTypeName, existing.ClrTypeName, StringComparison.Ordinal)
                   && string.Equals(FromProject, existing.FromProject, StringComparison.Ordinal)
                   && string.Equals(Category, NullIfWhiteSpace(existing.Category), StringComparison.Ordinal)
                   && string.Equals(SchemaHash, existing.SchemaHash, StringComparison.Ordinal)
                   && string.Equals(ReloadBehavior, existing.ReloadBehavior, StringComparison.Ordinal);
        }

        private static string? NullIfWhiteSpace(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static int ResolvePublishedSchemaVersion(
            ConfigurationDefinition definition,
            PublishedDefinitionDto? existing)
        {
            if (existing is null)
            {
                return Math.Max(definition.SchemaVersion, 1);
            }

            var currentVersion = Math.Max(Math.Max(existing.SchemaVersion, definition.SchemaVersion), 1);
            return string.Equals(existing.SchemaHash, definition.SchemaHash, StringComparison.Ordinal)
                ? currentVersion
                : currentVersion + 1;
        }

        public ConfigurationDefinition ToDefinition()
        {
            return ConfigurationDefinitionSchemaCodec.DeserializeDefinition(
                DefinitionKey,
                SectionPath,
                DisplayName,
                Description,
                ClrTypeName,
                FromProject,
                Category,
                SchemaVersion,
                SchemaHash,
                Enum.Parse<ConfigurationReloadBehavior>(ReloadBehavior),
                SchemaJson,
                ConfigurationDefinitionOrigin.PublishedMetadata);
        }
    }

    private sealed record HistoryDto
    {
        public string HistoryId { get; init; } = "";

        public string DefinitionKey { get; init; } = "";

        public string LogicalPath { get; init; } = "";

        public string? ConfigurationPath { get; init; }

        public string TargetKind { get; init; } = "";

        public string? SourceProviderType { get; init; }

        public string? SourceDisplayName { get; init; }

        public string? SourcePhysicalPath { get; init; }

        public string? SourceConfigurationPath { get; init; }

        public string MutationKind { get; init; } = "";

        public string Granularity { get; init; } = "";

        public string State { get; init; } = "";

        public ConfigurationStoredValue? OldValue { get; init; }

        public ConfigurationStoredValue NewValue { get; init; } = ConfigurationStoredValue.Null;

        public long Version { get; init; }

        public string? SourceRevisionBefore { get; init; }

        public string? SourceRevisionAfter { get; init; }

        public int SchemaVersion { get; init; }

        public DateTimeOffset ModifiedTime { get; init; }

        public string? ModifierId { get; init; }

        public string? ModifierName { get; init; }

        public string? Reason { get; init; }

        public string? MutationGroupId { get; init; }

        public static HistoryDto FromHistory(ConfigurationValueHistory history)
        {
            return new HistoryDto
            {
                HistoryId = history.HistoryId,
                DefinitionKey = history.DefinitionKey,
                LogicalPath = history.LogicalPath.ToCanonicalString(),
                ConfigurationPath = history.ConfigurationPath,
                TargetKind = history.TargetKind.ToString(),
                SourceProviderType = history.SourceProviderType,
                SourceDisplayName = history.SourceDisplayName,
                SourcePhysicalPath = history.SourcePhysicalPath,
                SourceConfigurationPath = history.SourceConfigurationPath,
                MutationKind = history.MutationKind.ToString(),
                Granularity = history.Granularity.ToString(),
                State = history.State.ToString(),
                OldValue = history.OldValue,
                NewValue = history.NewValue,
                Version = history.Version,
                SourceRevisionBefore = history.SourceRevisionBefore,
                SourceRevisionAfter = history.SourceRevisionAfter,
                SchemaVersion = history.SchemaVersion,
                ModifiedTime = history.ModifiedTime,
                ModifierId = history.ModifierId,
                ModifierName = history.ModifierName,
                Reason = history.Reason,
                MutationGroupId = history.MutationGroupId
            };
        }

        public ConfigurationValueHistory ToHistory()
        {
            return new ConfigurationValueHistory
            {
                HistoryId = HistoryId,
                DefinitionKey = DefinitionKey,
                LogicalPath = string.IsNullOrWhiteSpace(LogicalPath) ? Models.LogicalPath.Root : Models.LogicalPath.Parse(LogicalPath),
                ConfigurationPath = ConfigurationPath,
                TargetKind = string.IsNullOrWhiteSpace(TargetKind)
                    ? ConfigurationMutationTargetKind.MonicaEffectiveStore
                    : Enum.Parse<ConfigurationMutationTargetKind>(TargetKind),
                SourceProviderType = SourceProviderType,
                SourceDisplayName = SourceDisplayName,
                SourcePhysicalPath = SourcePhysicalPath,
                SourceConfigurationPath = SourceConfigurationPath,
                MutationKind = Enum.Parse<ConfigurationMutationKind>(MutationKind),
                Granularity = Enum.Parse<ConfigurationMutationGranularity>(Granularity),
                State = Enum.Parse<ConfigurationValueState>(State),
                OldValue = OldValue,
                NewValue = NewValue,
                Version = Version,
                SourceRevisionBefore = SourceRevisionBefore,
                SourceRevisionAfter = SourceRevisionAfter,
                SchemaVersion = SchemaVersion,
                ModifiedTime = ModifiedTime,
                ModifierId = ModifierId,
                ModifierName = ModifierName,
                Reason = Reason,
                MutationGroupId = MutationGroupId
            };
        }
    }

    private sealed record GroupDto
    {
        public string GroupId { get; init; } = "";

        public string Label { get; init; } = "";

        public string? Reason { get; init; }

        public IReadOnlyList<string> DefinitionKeys { get; init; } = [];

        public int MutationCount { get; init; }

        public DateTimeOffset CreatedTime { get; init; }

        public string? ModifierId { get; init; }

        public string? ModifierName { get; init; }

        public DateTimeOffset? RolledBackTime { get; init; }

        public string? RolledBackGroupId { get; init; }

        public string Status { get; init; } = "";

        public static GroupDto FromGroup(ConfigurationMutationGroup group)
        {
            return new GroupDto
            {
                GroupId = group.GroupId,
                Label = group.Label,
                Reason = group.Reason,
                DefinitionKeys = group.DefinitionKeys,
                MutationCount = group.MutationCount,
                CreatedTime = group.CreatedTime,
                ModifierId = group.ModifierId,
                ModifierName = group.ModifierName,
                RolledBackTime = group.RolledBackTime,
                RolledBackGroupId = group.RolledBackGroupId,
                Status = group.Status.ToString()
            };
        }

        public ConfigurationMutationGroup ToGroup()
        {
            return new ConfigurationMutationGroup
            {
                GroupId = GroupId,
                Label = Label,
                Reason = Reason,
                DefinitionKeys = DefinitionKeys,
                MutationCount = MutationCount,
                CreatedTime = CreatedTime,
                ModifierId = ModifierId,
                ModifierName = ModifierName,
                RolledBackTime = RolledBackTime,
                RolledBackGroupId = RolledBackGroupId,
                Status = Enum.Parse<ConfigurationMutationGroupStatus>(Status)
            };
        }
    }
}
