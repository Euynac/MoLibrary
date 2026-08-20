using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Monica.StateStore.Queries;

namespace Monica.Dapr.Services.Support;

internal static class DaprStateQueryRenderer
{
    private static readonly JsonSerializerOptions PROTOCOL_JSON_OPTIONS = CreateProtocolJsonOptions();

    public static string Render(
        StateQueryDefinition definition,
        JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(serializerOptions);

        var document = new Dictionary<string, object?>();
        if (definition.Filter is not null)
        {
            document["filter"] = RenderFilter(definition.Filter, serializerOptions);
        }

        if (definition.Sorting.Count != 0)
        {
            document["sort"] = definition.Sorting.Select(sorting => new Dictionary<string, object?>
            {
                ["key"] = ResolvePath(sorting.Property, serializerOptions),
                ["order"] = sorting.Order is Ordering.Ascending ? "ASC" : "DESC"
            }).ToArray();
        }

        if (definition.Paging is not null)
        {
            var page = new Dictionary<string, object?>();
            if (definition.Paging.Limit is { } limit)
            {
                page["limit"] = limit;
            }

            if (definition.Paging.Token is { } token)
            {
                page["token"] = token;
            }

            document["page"] = page;
        }

        // The host JSON contract controls document paths and comparison values, not Dapr's query-envelope property
        // names. Serializing the envelope with the host settings would apply its DictionaryKeyPolicy to operators
        // such as EQ and AND.
        return JsonSerializer.Serialize(document, PROTOCOL_JSON_OPTIONS);
    }

    private static object RenderFilter(
        StateFilterNode filter,
        JsonSerializerOptions serializerOptions)
    {
        return filter switch
        {
            StateComparisonFilterNode comparison => new Dictionary<string, object?>
            {
                [GetComparisonOperator(comparison.Operator)] = new Dictionary<string, object?>
                {
                    [ResolvePath(comparison.Property, serializerOptions)] =
                        SerializeComparisonValue(comparison.Operator, comparison.Value, serializerOptions)
                }
            },
            StateGroupFilterNode group => new Dictionary<string, object?>
            {
                [GetGroupOperator(group.Operator)] = group.Children
                    .Select(child => RenderFilter(child, serializerOptions))
                    .ToArray()
            },
            _ => throw new InvalidOperationException($"Unsupported state filter node '{filter.GetType().Name}'.")
        };
    }

    private static object SerializeComparisonValue(
        StateComparisonOperator comparisonOperator,
        object? value,
        JsonSerializerOptions serializerOptions)
    {
        if (comparisonOperator is StateComparisonOperator.In && value is string[] values)
        {
            // Dapr requires IN values to remain a protocol array. Serializing the array through a host contract that
            // preserves references would wrap it in $id/$values metadata, so apply host converters to each scalar and
            // let the neutral protocol serializer own the container.
            return values
                .Select(item => JsonSerializer.SerializeToElement(item, serializerOptions))
                .ToArray();
        }

        return value is null
            ? JsonSerializer.SerializeToElement<object?>(null, serializerOptions)
            : JsonSerializer.SerializeToElement(value, value.GetType(), serializerOptions);
    }

    private static string GetComparisonOperator(StateComparisonOperator comparisonOperator)
    {
        return comparisonOperator switch
        {
            StateComparisonOperator.Equal => "EQ",
            StateComparisonOperator.In => "IN",
            StateComparisonOperator.GreaterThan => "GT",
            StateComparisonOperator.GreaterThanOrEqual => "GTE",
            StateComparisonOperator.LessThan => "LT",
            StateComparisonOperator.LessThanOrEqual => "LTE",
            _ => throw new ArgumentOutOfRangeException(nameof(comparisonOperator), comparisonOperator, null)
        };
    }

    private static string GetGroupOperator(StateGroupOperator groupOperator)
    {
        return groupOperator switch
        {
            StateGroupOperator.And => "AND",
            StateGroupOperator.Or => "OR",
            _ => throw new ArgumentOutOfRangeException(nameof(groupOperator), groupOperator, null)
        };
    }

    private static string ResolvePath(
        LambdaExpression expression,
        JsonSerializerOptions serializerOptions)
    {
        var members = new Stack<PropertyInfo>();
        Expression? current = expression.Body;
        while (current is MemberExpression { Member: PropertyInfo property } member)
        {
            members.Push(property);
            current = member.Expression;
        }

        if (current != expression.Parameters[0] || members.Count == 0)
        {
            throw new ArgumentException(
                $"State query expression '{expression}' must be a direct property path.",
                nameof(expression));
        }

        var names = new List<string>(members.Count);
        var currentType = expression.Parameters[0].Type;
        while (members.TryPop(out var property))
        {
            var typeInfo = serializerOptions.GetTypeInfo(currentType);
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                throw new InvalidOperationException(
                    $"The host JSON contract does not expose '{currentType.FullName}' as an object document.");
            }

            var jsonProperty = typeInfo.Properties.FirstOrDefault(candidate => candidate.Get is not null
                && candidate.AttributeProvider is PropertyInfo info
                && info == property);
            if (jsonProperty is null)
            {
                throw new InvalidOperationException(
                    $"Property '{currentType.FullName}.{property.Name}' is ignored or unavailable in the host JSON contract.");
            }

            names.Add(jsonProperty.Name);
            currentType = property.PropertyType;
        }

        return string.Join('.', names);
    }

    private static JsonSerializerOptions CreateProtocolJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            MaxDepth = int.MaxValue,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        options.MakeReadOnly();
        return options;
    }
}
