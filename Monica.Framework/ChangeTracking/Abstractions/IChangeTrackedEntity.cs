using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Monica.Repository.Entity.Abstractions;

namespace Monica.Framework.ChangeTracking.Abstractions;

public interface IChangeTrackedEntity
{
    /// <summary>
    /// Extract the current state into traceable chain information
    /// </summary>
    /// <returns></returns>
    public string GetCurTracingData()
    {
        return JsonSerializer.Serialize((object)this, ChangeTrackingJsonPolicy.TracingDataOptions);
    }
}

internal static class ChangeTrackingJsonPolicy
{
    internal static JsonSerializerOptions TracingDataOptions { get; } = CreateTracingDataOptions();

    private static JsonSerializerOptions CreateTracingDataOptions()
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            TypeInfoResolver = new IgnorePropertyContractResolver()
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    // https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-7/#example-conditional-serialization
    private sealed class IgnorePropertyContractResolver : DefaultJsonTypeInfoResolver
    {
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var typeInfo = base.GetTypeInfo(type, options);

            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return typeInfo;
            }

            foreach (var property in typeInfo.Properties)
            {
                if (property.PropertyType == typeof(string))
                {
                    property.ShouldSerialize = (_, value) => !string.IsNullOrEmpty((string?)value);
                }
                else if (property.Name is "Id" or nameof(IHasExtraProperties.ExtraProperties))
                {
                    property.ShouldSerialize = (_, _) => false;
                }
            }

            return typeInfo;
        }
    }
}
