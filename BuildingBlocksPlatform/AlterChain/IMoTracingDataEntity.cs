using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BuildingBlocksPlatform.Repository.EntityInterfaces;

namespace BuildingBlocksPlatform.AlterChain;

public interface IMoTracingDataEntity
{
    public static readonly JsonSerializerOptions TRACING_DATA_JSON_OPTIONS = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        TypeInfoResolver = new IgnorePropertyContractResolver()
    };
    //https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-7/#example-conditional-serialization
    internal class IgnorePropertyContractResolver : DefaultJsonTypeInfoResolver
    {
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var typeInfo = base.GetTypeInfo(type, options);

            if (typeInfo.Kind == JsonTypeInfoKind.Object)
            {
                foreach (var property in typeInfo.Properties)
                {
                    if (property.PropertyType == typeof(string))
                    {
                        property.ShouldSerialize = (_, value) => !string.IsNullOrEmpty((string?) value);
                    }
                    else if (property.Name is "Id" or nameof(IHasExtraProperties.ExtraProperties)) // or nameof(ConcurrencyStamp)
                    {
                        property.ShouldSerialize = (_, _) => false;
                    }
                }
            }

            return typeInfo;
        }
    }
    /// <summary>
    /// 将当前状态提取为可追踪链信息
    /// </summary>
    /// <returns></returns>
    public string GetCurTracingData()
    {
        return JsonSerializer.Serialize((object)this, TRACING_DATA_JSON_OPTIONS);
    }
}