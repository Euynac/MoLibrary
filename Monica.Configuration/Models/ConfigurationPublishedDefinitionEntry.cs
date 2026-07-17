using System.Text.Json;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Serialization;

namespace Monica.Configuration.Models;

/// <summary>
/// Represents one independently materialized published configuration definition record.
/// </summary>
public sealed class ConfigurationPublishedDefinitionEntry
{
    private ConfigurationPublishedDefinitionEntry(
        ConfigurationPublishedDefinitionRecord metadata,
        ConfigurationDefinition? definition,
        ConfigurationDefinitionMetadataDiagnostic? diagnostic)
    {
        Metadata = metadata;
        Definition = definition;
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Gets the raw persisted metadata envelope.
    /// </summary>
    public ConfigurationPublishedDefinitionRecord Metadata { get; }

    /// <summary>
    /// Gets the fully materialized definition when the record is valid.
    /// </summary>
    public ConfigurationDefinition? Definition { get; }

    /// <summary>
    /// Gets the materialization diagnostic when the record is invalid.
    /// </summary>
    public ConfigurationDefinitionMetadataDiagnostic? Diagnostic { get; }

    /// <summary>
    /// Gets whether this entry contains a safe, complete definition.
    /// </summary>
    public bool IsAvailable => Definition is not null;

    /// <summary>
    /// Materializes a raw published record without allowing a data problem to escape into sibling records.
    /// </summary>
    /// <param name="metadata">The raw persisted record.</param>
    /// <returns>An available definition or a structured diagnostic.</returns>
    public static ConfigurationPublishedDefinitionEntry Materialize(ConfigurationPublishedDefinitionRecord metadata)
    {
        if (TryGetEnvelopeProblem(metadata, out var envelopeProblem))
        {
            return Unavailable(
                metadata,
                ConfigurationDefinitionMetadataIssueKind.InvalidEnvelope,
                nameof(ConfigurationDefinitionMetadataIssueKind.InvalidEnvelope),
                envelopeProblem!);
        }

        if (!Enum.TryParse<ConfigurationReloadBehavior>(metadata.ReloadBehavior, ignoreCase: false, out var reloadBehavior)
            || !Enum.IsDefined(reloadBehavior))
        {
            return Unavailable(
                metadata,
                ConfigurationDefinitionMetadataIssueKind.InvalidReloadBehavior,
                nameof(ConfigurationReloadBehavior),
                $"Persisted reload behavior '{metadata.ReloadBehavior}' is not supported.");
        }

        try
        {
            var definition = ConfigurationDefinitionSchemaCodec.DeserializeDefinition(
                metadata.DefinitionKey,
                metadata.SectionPath,
                DisplayNameOrKey(metadata),
                metadata.Description,
                metadata.ClrTypeName,
                metadata.FromProject,
                metadata.Category,
                metadata.SchemaVersion,
                metadata.SchemaHash,
                reloadBehavior,
                metadata.SchemaJson,
                ConfigurationDefinitionOrigin.PublishedMetadata) with
            {
                DefinitionRevision = metadata.DefinitionRevision
            };

            var computedSchemaHash = ConfigurationDefinitionSchemaCodec.ComputeSchemaHash(
                definition.DefinitionKey,
                definition.SectionPath,
                definition.Root);
            if (!string.Equals(metadata.SchemaHash, computedSchemaHash, StringComparison.Ordinal))
            {
                return Unavailable(
                    metadata,
                    ConfigurationDefinitionMetadataIssueKind.SchemaHashMismatch,
                    nameof(ConfigurationDefinitionMetadataIssueKind.SchemaHashMismatch),
                    $"Persisted schema hash '{metadata.SchemaHash}' does not match the canonical schema payload hash "
                    + $"'{computedSchemaHash}'. Republish the definition metadata before using it.");
            }

            return new ConfigurationPublishedDefinitionEntry(metadata, definition, null);
        }
        catch (ConfigurationPersistedSchemaException ex)
        {
            return Unavailable(metadata, ex.Kind, ex.GetType().FullName ?? ex.GetType().Name, Describe(ex), ex.SchemaPath);
        }
        catch (JsonException ex)
        {
            return Unavailable(
                metadata,
                ConfigurationDefinitionMetadataIssueKind.InvalidSchemaJson,
                ex.GetType().FullName ?? ex.GetType().Name,
                Describe(ex),
                jsonPath: ex.Path);
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                   or FormatException
                                   or ArgumentException
                                   or NotSupportedException
                                   or NullReferenceException)
        {
            return Unavailable(
                metadata,
                ConfigurationDefinitionMetadataIssueKind.InvalidSchema,
                ex.GetType().FullName ?? ex.GetType().Name,
                Describe(ex));
        }
    }

    /// <summary>
    /// Creates an unavailable entry when the outer metadata envelope cannot be parsed.
    /// </summary>
    /// <param name="metadata">The trustworthy fallback metadata.</param>
    /// <param name="error">The envelope read failure.</param>
    /// <param name="recommendedAction">The operator action that can repair the envelope problem.</param>
    /// <returns>An unavailable entry carrying the read diagnostic.</returns>
    public static ConfigurationPublishedDefinitionEntry FromEnvelopeFailure(
        ConfigurationPublishedDefinitionRecord metadata,
        Exception error,
        ConfigurationDefinitionMetadataRepairAction recommendedAction =
            ConfigurationDefinitionMetadataRepairAction.RepublishDefinition)
    {
        return Unavailable(
            metadata,
            ConfigurationDefinitionMetadataIssueKind.InvalidEnvelope,
            error.GetType().FullName ?? error.GetType().Name,
            Describe(error),
            jsonPath: error is JsonException jsonError ? jsonError.Path : null,
            recommendedAction: recommendedAction);
    }

    /// <summary>
    /// Gets the materialized definition or throws a typed exception carrying the diagnostic.
    /// </summary>
    /// <returns>The complete published definition.</returns>
    /// <exception cref="ConfigurationDefinitionMetadataUnavailableException">
    /// The persisted record cannot be materialized safely.
    /// </exception>
    public ConfigurationDefinition RequireDefinition()
    {
        return Definition
               ?? throw new ConfigurationDefinitionMetadataUnavailableException(
                   Metadata.DefinitionKey,
                   Diagnostic ?? throw new InvalidOperationException("Unavailable definition metadata has no diagnostic."));
    }

    private static ConfigurationPublishedDefinitionEntry Unavailable(
        ConfigurationPublishedDefinitionRecord metadata,
        ConfigurationDefinitionMetadataIssueKind kind,
        string errorType,
        string message,
        string? schemaPath = null,
        string? jsonPath = null,
        ConfigurationDefinitionMetadataRepairAction recommendedAction =
            ConfigurationDefinitionMetadataRepairAction.RepublishDefinition)
    {
        return new ConfigurationPublishedDefinitionEntry(
            metadata,
            null,
            new ConfigurationDefinitionMetadataDiagnostic
            {
                Kind = kind,
                StoreKey = metadata.StoreKey,
                ErrorType = errorType,
                Message = message,
                SchemaPath = schemaPath,
                JsonPath = jsonPath,
                RecommendedAction = recommendedAction
            });
    }

    private static bool TryGetEnvelopeProblem(
        ConfigurationPublishedDefinitionRecord metadata,
        out string? problem)
    {
        if (string.IsNullOrWhiteSpace(metadata.StoreKey))
        {
            problem = "Persisted definition metadata is missing its metadata store identity.";
            return true;
        }

        if (string.IsNullOrWhiteSpace(metadata.DefinitionKey))
        {
            problem = "Persisted definition metadata is missing its definition key.";
            return true;
        }

        if (string.IsNullOrWhiteSpace(metadata.SectionPath))
        {
            problem = $"Persisted definition '{metadata.DefinitionKey}' is missing its configuration section path.";
            return true;
        }

        if (string.IsNullOrWhiteSpace(metadata.ClrTypeName))
        {
            problem = $"Persisted definition '{metadata.DefinitionKey}' is missing its root CLR type.";
            return true;
        }

        if (string.IsNullOrWhiteSpace(metadata.FromProject))
        {
            problem = $"Persisted definition '{metadata.DefinitionKey}' is missing its publishing project.";
            return true;
        }

        if (metadata.SchemaVersion < 1)
        {
            problem = $"Persisted definition '{metadata.DefinitionKey}' has invalid schema version {metadata.SchemaVersion}.";
            return true;
        }

        if (metadata.DefinitionRevision < 1)
        {
            problem = $"Persisted definition '{metadata.DefinitionKey}' has invalid definition revision {metadata.DefinitionRevision}.";
            return true;
        }

        if (string.IsNullOrWhiteSpace(metadata.SchemaHash))
        {
            problem = $"Persisted definition '{metadata.DefinitionKey}' is missing its schema hash.";
            return true;
        }

        if (string.IsNullOrWhiteSpace(metadata.SchemaJson))
        {
            problem = $"Persisted definition '{metadata.DefinitionKey}' has empty schema JSON.";
            return true;
        }

        problem = null;
        return false;
    }

    private static string DisplayNameOrKey(ConfigurationPublishedDefinitionRecord metadata)
    {
        return string.IsNullOrWhiteSpace(metadata.DisplayName) ? metadata.DefinitionKey : metadata.DisplayName;
    }

    private static string Describe(Exception error)
    {
        var messages = new List<string>();
        for (var current = error; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message)
                && !messages.Contains(current.Message, StringComparer.Ordinal))
            {
                messages.Add(current.Message);
            }
        }

        return string.Join(" --> ", messages);
    }
}
