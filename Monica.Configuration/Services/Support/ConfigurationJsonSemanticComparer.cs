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
        var leftProperties = left.EnumerateObject().ToArray();
        var rightProperties = right.EnumerateObject().ToArray();
        if (leftProperties.Length != rightProperties.Length)
        {
            return false;
        }

        var rightByName = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in rightProperties)
        {
            if (!rightByName.TryAdd(property.Name, property.Value))
            {
                return false;
            }
        }

        var leftNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in leftProperties)
        {
            if (!leftNames.Add(property.Name)
                || !rightByName.TryGetValue(property.Name, out var rightValue)
                || !Equals(property.Value, rightValue))
            {
                return false;
            }
        }

        return true;
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
