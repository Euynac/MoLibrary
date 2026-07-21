using System.Linq.Expressions;
using System.Reflection;
using ExpressionDebugger;
using Mapster;
using Monica.Core.ObjectMapping.Models;
using Monica.Tool.Extensions;

namespace Monica.Core.ObjectMapping.Providers.Mapster;

internal sealed class MapsterMappingInspector(TypeAdapterConfig config)
{
    public IReadOnlyList<ObjectMapperInfo> GetMappings()
    {
        var inspectionConfig = config.Clone();
        inspectionConfig.SelfContainedCodeGeneration = true;

        var buildAdapterMethod = GetBuildAdapterMethod();
        var mappings = new List<ObjectMapperInfo>();

        foreach (var rule in inspectionConfig.RuleMap)
        {
            var sourceType = rule.Key.Source;
            var destinationType = rule.Key.Destination;
            var adapterBuilder = buildAdapterMethod.MakeGenericMethod(sourceType)
                .Invoke(null, [GetDefaultValue(sourceType), inspectionConfig])
                ?? throw new InvalidOperationException("Failed to create the Mapster adapter builder.");
            var createMapExpression = typeof(ITypeAdapterBuilder<>)
                .MakeGenericType(sourceType)
                .GetMethod("CreateMapExpression")
                ?? throw new InvalidOperationException("CreateMapExpression method was not found.");
            var mapExpression = createMapExpression
                .MakeGenericMethod(destinationType)
                .Invoke(adapterBuilder, null)
                ?? throw new InvalidOperationException("Failed to generate the mapping expression.");

            mappings.Add(new ObjectMapperInfo
            {
                SourceType = sourceType.GetCleanFullName(),
                DestinationType = destinationType.GetCleanFullName(),
                MapExpression = (string)ExpressionTranslatorExtensions.ToScript((Expression)mapExpression)
            });
        }

        return mappings;
    }

    private static MethodInfo GetBuildAdapterMethod()
    {
        return typeof(TypeAdapter).GetMethods(BindingFlags.Public | BindingFlags.Static)
                   .SingleOrDefault(method =>
                       method is { Name: "BuildAdapter", IsGenericMethod: true } &&
                       method.GetParameters() is [{}, { ParameterType: not null } parameters] &&
                       parameters.ParameterType == typeof(TypeAdapterConfig))
               ?? throw new InvalidOperationException("BuildAdapter<T> method was not found.");
    }

    private static object? GetDefaultValue(Type type)
    {
        return typeof(MapsterMappingInspector)
            .GetMethod(nameof(GetDefaultValueGeneric), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(type)
            .Invoke(null, null);
    }

    private static T? GetDefaultValueGeneric<T>()
    {
        return default;
    }
}
