using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Patches physical JSON configuration files for source-targeted mutations.
/// </summary>
internal sealed class ConfigurationJsonFileSourceWriter : IConfigurationJsonFileSourceWriter
{
    private static readonly JsonSerializerOptions WRITE_OPTIONS = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonDocumentOptions DOCUMENT_OPTIONS = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Applies a source-targeted JSON mutation.
    /// </summary>
    public async Task<ConfigurationJsonFileWriteResult> WriteAsync(
        ConfigurationSourceDescriptor source,
        string configurationPath,
        ConfigurationMutationKind mutationKind,
        ConfigurationStoredValue value,
        string? expectedRevision,
        CancellationToken cancellationToken)
    {
        if (source.Kind != ConfigurationSourceKind.JsonFile || string.IsNullOrWhiteSpace(source.PhysicalPath))
        {
            throw new InvalidOperationException($"Configuration source '{source.DisplayName}' is not a writable JSON file.");
        }

        if (!source.IsWritable)
        {
            throw new InvalidOperationException(source.ReadOnlyReason ?? $"Configuration source '{source.DisplayName}' is read-only.");
        }

        var originalText = File.Exists(source.PhysicalPath)
            ? await File.ReadAllTextAsync(source.PhysicalPath, cancellationToken)
            : "{}";
        var oldRevision = ComputeRevision(originalText);
        if (!string.IsNullOrWhiteSpace(expectedRevision)
            && !string.Equals(oldRevision, expectedRevision, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigurationConcurrencyConflictException(
                $"Expected source revision {expectedRevision} for '{source.DisplayName}', but current revision is {oldRevision}.");
        }

        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(originalText) ? "{}" : originalText, documentOptions: DOCUMENT_OPTIONS)
                   ?? new JsonObject();
        var pathSegments = configurationPath.Split(':', StringSplitOptions.RemoveEmptyEntries);
        var oldValue = Read(root, pathSegments);
        if (mutationKind == ConfigurationMutationKind.Remove)
        {
            Remove(root, pathSegments);
        }
        else
        {
            var newValue = JsonNode.Parse(value.Json, documentOptions: DOCUMENT_OPTIONS);
            Set(root, pathSegments, newValue);
        }

        var updatedText = root.ToJsonString(WRITE_OPTIONS);
        Directory.CreateDirectory(Path.GetDirectoryName(source.PhysicalPath)!);
        await File.WriteAllTextAsync(source.PhysicalPath, updatedText, cancellationToken);
        return new ConfigurationJsonFileWriteResult
        {
            OldValue = oldValue,
            NewValue = mutationKind == ConfigurationMutationKind.Remove
                ? ConfigurationStoredValue.Null
                : Read(root, pathSegments) ?? ConfigurationStoredValue.Null,
            OldRevision = oldRevision,
            NewRevision = ComputeRevision(updatedText),
            ModifiedTime = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Gets the current source content revision.
    /// </summary>
    public async Task<string?> GetRevisionAsync(ConfigurationSourceDescriptor source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.PhysicalPath) || !File.Exists(source.PhysicalPath))
        {
            return null;
        }

        return ComputeRevision(await File.ReadAllTextAsync(source.PhysicalPath, cancellationToken));
    }

    private static ConfigurationStoredValue? Read(JsonNode? root, IReadOnlyList<string> segments)
    {
        var current = root;
        foreach (var segment in segments)
        {
            current = current switch
            {
                JsonObject jsonObject => jsonObject[segment],
                JsonArray jsonArray when int.TryParse(segment, out var index) && index >= 0 && index < jsonArray.Count => jsonArray[index],
                _ => null
            };

            if (current is null)
            {
                return null;
            }
        }

        return ConfigurationStoredValue.FromJson(current!.ToJsonString());
    }

    private static void Set(JsonNode root, IReadOnlyList<string> segments, JsonNode? value)
    {
        if (segments.Count == 0)
        {
            throw new ConfigurationValidationFailedException("Source mutations require a non-root configuration path.");
        }

        var parent = GetOrCreateParent(root, segments);
        var last = segments[^1];
        switch (parent)
        {
            case JsonObject jsonObject:
                jsonObject[last] = value?.DeepClone();
                break;
            case JsonArray jsonArray when int.TryParse(last, out var index):
                EnsureArraySize(jsonArray, index);
                jsonArray[index] = value?.DeepClone();
                break;
            default:
                throw new ConfigurationValidationFailedException($"Configuration path segment '{last}' cannot be written to the target JSON source.");
        }
    }

    private static void Remove(JsonNode root, IReadOnlyList<string> segments)
    {
        if (segments.Count == 0)
        {
            throw new ConfigurationValidationFailedException("Source mutations require a non-root configuration path.");
        }

        var parent = GetExistingParent(root, segments);
        if (parent is null)
        {
            return;
        }

        var last = segments[^1];
        switch (parent)
        {
            case JsonObject jsonObject:
                jsonObject.Remove(last);
                break;
            case JsonArray jsonArray when int.TryParse(last, out var index) && index >= 0 && index < jsonArray.Count:
                jsonArray.RemoveAt(index);
                break;
        }
    }

    private static JsonNode GetOrCreateParent(JsonNode root, IReadOnlyList<string> segments)
    {
        var current = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var segment = segments[i];
            var nextIsArray = int.TryParse(segments[i + 1], out _);
            current = current switch
            {
                JsonObject jsonObject => GetOrCreateObjectChild(jsonObject, segment, nextIsArray),
                JsonArray jsonArray when int.TryParse(segment, out var index) => GetOrCreateArrayChild(jsonArray, index, nextIsArray),
                _ => throw new ConfigurationValidationFailedException($"Configuration path segment '{segment}' cannot be traversed in the target JSON source.")
            };
        }

        return current;
    }

    private static JsonNode? GetExistingParent(JsonNode root, IReadOnlyList<string> segments)
    {
        var current = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var segment = segments[i];
            current = current switch
            {
                JsonObject jsonObject => jsonObject[segment],
                JsonArray jsonArray when int.TryParse(segment, out var index) && index >= 0 && index < jsonArray.Count => jsonArray[index],
                _ => null
            };

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static JsonNode GetOrCreateObjectChild(JsonObject jsonObject, string key, bool nextIsArray)
    {
        if (jsonObject[key] is { } child)
        {
            return child;
        }

        child = nextIsArray ? new JsonArray() : new JsonObject();
        jsonObject[key] = child;
        return child;
    }

    private static JsonNode GetOrCreateArrayChild(JsonArray jsonArray, int index, bool nextIsArray)
    {
        EnsureArraySize(jsonArray, index);
        if (jsonArray[index] is { } child)
        {
            return child;
        }

        child = nextIsArray ? new JsonArray() : new JsonObject();
        jsonArray[index] = child;
        return child;
    }

    private static void EnsureArraySize(JsonArray jsonArray, int index)
    {
        if (index < 0)
        {
            throw new ConfigurationValidationFailedException("Array indexes in configuration paths must not be negative.");
        }

        while (jsonArray.Count <= index)
        {
            jsonArray.Add(null);
        }
    }

    private static string ComputeRevision(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
