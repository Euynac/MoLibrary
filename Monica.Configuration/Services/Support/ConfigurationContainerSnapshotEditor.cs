using System.Text.Json.Nodes;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Edits plain JSON container snapshots by logical path.
/// </summary>
public sealed class ConfigurationContainerSnapshotEditor
{
    /// <summary>
    /// Replaces one descendant value inside a container snapshot.
    /// </summary>
    /// <param name="container">The container snapshot payload.</param>
    /// <param name="containerPath">The logical path represented by the snapshot.</param>
    /// <param name="targetPath">The descendant logical path to patch.</param>
    /// <param name="newValue">The replacement value payload.</param>
    /// <returns>The patched container snapshot payload.</returns>
    public ConfigurationStoredValue Patch(
        ConfigurationStoredValue container,
        LogicalPath containerPath,
        LogicalPath targetPath,
        ConfigurationStoredValue newValue)
    {
        var root = ParseContainer(container, containerPath);
        if (newValue.Kind != ConfigurationStoredValueKind.PlainJson || newValue.PlainJson is null)
        {
            throw new ConfigurationValidationFailedException(
                $"Value '{targetPath}' cannot patch container snapshot '{containerPath}' because it is not plain JSON.");
        }

        PatchNode(root, GetRelativeSegments(containerPath, targetPath), JsonNode.Parse(newValue.PlainJson));
        return ConfigurationStoredValue.Plain(root.ToJsonString());
    }

    /// <summary>
    /// Removes one descendant value from a container snapshot.
    /// </summary>
    /// <param name="container">The container snapshot payload.</param>
    /// <param name="containerPath">The logical path represented by the snapshot.</param>
    /// <param name="targetPath">The descendant logical path to remove.</param>
    /// <returns>The patched container snapshot payload.</returns>
    public ConfigurationStoredValue Remove(ConfigurationStoredValue container, LogicalPath containerPath, LogicalPath targetPath)
    {
        var root = ParseContainer(container, containerPath);
        RemoveNode(root, GetRelativeSegments(containerPath, targetPath));
        return ConfigurationStoredValue.Plain(root.ToJsonString());
    }

    private static IReadOnlyList<ConfigurationPathSegment> GetRelativeSegments(LogicalPath containerPath, LogicalPath targetPath)
    {
        return targetPath.Segments.Skip(containerPath.Depth).ToArray();
    }

    private static JsonNode ParseContainer(ConfigurationStoredValue container, LogicalPath containerPath)
    {
        if (container.Kind != ConfigurationStoredValueKind.PlainJson || container.PlainJson is null)
        {
            throw new ConfigurationValidationFailedException(
                $"Container snapshot '{containerPath}' cannot be patched because it is not plain JSON.");
        }

        return JsonNode.Parse(container.PlainJson) ?? new JsonObject();
    }

    private static void PatchNode(JsonNode root, IReadOnlyList<ConfigurationPathSegment> segments, JsonNode? value)
    {
        if (segments.Count == 0)
        {
            throw new ConfigurationValidationFailedException("Cannot patch an empty relative path.");
        }

        var current = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            current = GetOrCreateChild(current, segments[i], segments[i + 1]);
        }

        SetChild(current, segments[^1], value);
    }

    private static void RemoveNode(JsonNode root, IReadOnlyList<ConfigurationPathSegment> segments)
    {
        if (segments.Count == 0)
        {
            throw new ConfigurationValidationFailedException("Cannot remove an empty relative path.");
        }

        var current = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var child = GetExistingChild(current, segments[i]);
            if (child is null)
            {
                return;
            }

            current = child;
        }

        RemoveChild(current, segments[^1]);
    }

    private static JsonNode GetOrCreateChild(JsonNode current, ConfigurationPathSegment segment, ConfigurationPathSegment nextSegment)
    {
        var shouldCreateArray = nextSegment is ListIndexSegment;
        return current switch
        {
            JsonObject currentObject => GetOrCreateObjectChild(currentObject, segment.Value, shouldCreateArray),
            JsonArray currentArray when int.TryParse(segment.Value, out var index) => GetOrCreateArrayChild(currentArray, index, shouldCreateArray),
            _ => throw new ConfigurationValidationFailedException($"Cannot patch through JSON node '{segment.Value}'.")
        };
    }

    private static JsonNode? GetExistingChild(JsonNode current, ConfigurationPathSegment segment)
    {
        return current switch
        {
            JsonObject currentObject => currentObject[segment.Value],
            JsonArray currentArray when int.TryParse(segment.Value, out var index)
                                        && index >= 0
                                        && index < currentArray.Count => currentArray[index],
            _ => null
        };
    }

    private static JsonNode GetOrCreateObjectChild(JsonObject currentObject, string name, bool createArray)
    {
        if (currentObject[name] is not null)
        {
            return currentObject[name]!;
        }

        JsonNode child = createArray ? new JsonArray() : new JsonObject();
        currentObject[name] = child;
        return child;
    }

    private static JsonNode GetOrCreateArrayChild(JsonArray currentArray, int index, bool createArray)
    {
        EnsureArraySize(currentArray, index);
        if (currentArray[index] is not null)
        {
            return currentArray[index]!;
        }

        JsonNode child = createArray ? new JsonArray() : new JsonObject();
        currentArray[index] = child;
        return child;
    }

    private static void SetChild(JsonNode current, ConfigurationPathSegment segment, JsonNode? value)
    {
        switch (current)
        {
            case JsonObject currentObject:
                currentObject[segment.Value] = value;
                break;
            case JsonArray currentArray when int.TryParse(segment.Value, out var index):
                EnsureArraySize(currentArray, index);
                currentArray[index] = value;
                break;
            default:
                throw new ConfigurationValidationFailedException($"Cannot patch JSON node '{segment.Value}'.");
        }
    }

    private static void RemoveChild(JsonNode current, ConfigurationPathSegment segment)
    {
        switch (current)
        {
            case JsonObject currentObject:
                currentObject.Remove(segment.Value);
                break;
            case JsonArray currentArray when int.TryParse(segment.Value, out var index)
                                         && index >= 0
                                         && index < currentArray.Count:
                currentArray[index] = null;
                break;
        }
    }

    private static void EnsureArraySize(JsonArray array, int index)
    {
        while (array.Count <= index)
        {
            array.Add(null);
        }
    }
}
