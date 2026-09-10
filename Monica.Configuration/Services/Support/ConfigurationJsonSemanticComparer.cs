using System.Text.Json;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Compares configuration JSON using Microsoft configuration semantics for object names and numeric values.
/// </summary>
internal static class ConfigurationJsonSemanticComparer
{
    public static bool Equals(string left, string right)
    {
        using var leftDocument = JsonDocument.Parse(left);
        using var rightDocument = JsonDocument.Parse(right);
        return Equals(leftDocument.RootElement, rightDocument.RootElement);
    }

    public static bool Equals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.Object => ObjectsEqual(left, right),
            JsonValueKind.Array => ArraysEqual(left, right),
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
            JsonValueKind.Number => NumbersEqual(left, right),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined => true,
            _ => false
        };
    }

    private static bool ObjectsEqual(JsonElement left, JsonElement right)
    {
        // Explicit null properties are equivalent to absent properties: Microsoft configuration binding
        // treats them identically, and runtime JSON reconstruction omits nulls while stored documents
        // keep them. Dropping null-valued properties on both sides before comparing keeps those
        // representations semantically equal.
        var leftByName = ToNonNullPropertyMap(left, out var leftValid);
        var rightByName = ToNonNullPropertyMap(right, out var rightValid);
        if (!leftValid || !rightValid || leftByName.Count != rightByName.Count)
        {
            return false;
        }

        foreach (var (name, value) in leftByName)
        {
            if (!rightByName.TryGetValue(name, out var rightValue) || !Equals(value, rightValue))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, JsonElement> ToNonNullPropertyMap(JsonElement obj, out bool hasNoDuplicates)
    {
        hasNoDuplicates = true;
        var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in obj.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            // Duplicate property names are case-insensitively ambiguous and never compare equal.
            if (!map.TryAdd(property.Name, property.Value))
            {
                hasNoDuplicates = false;
                return map;
            }
        }

        return map;
    }

    private static bool ArraysEqual(JsonElement left, JsonElement right)
    {
        var leftItems = left.EnumerateArray().ToArray();
        var rightItems = right.EnumerateArray().ToArray();
        return leftItems.Length == rightItems.Length
               && leftItems.Zip(rightItems).All(static pair => Equals(pair.First, pair.Second));
    }

    private static bool NumbersEqual(JsonElement left, JsonElement right)
    {
        if (left.TryGetDecimal(out var leftDecimal) && right.TryGetDecimal(out var rightDecimal))
        {
            return leftDecimal == rightDecimal;
        }

        return left.TryGetDouble(out var leftDouble)
               && right.TryGetDouble(out var rightDouble)
               && leftDouble.Equals(rightDouble);
    }
}
