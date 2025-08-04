using System.Reflection;
using ExpressionDebugger;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Core.Features.MoMapper;

public static class MapperExtensions
{
    /// <summary>
    ///  展示Mapper的映射表达式
    /// </summary>
    /// <typeparam name="TDst"></typeparam>
    /// <typeparam name="TSrc"></typeparam>
    /// <param name="mapper"></param>
    /// <param name="src"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static void MapDebug<TSrc, TDst>(this IMapper mapper, TSrc src, ILogger? logger = null)
    {
        var script = src.BuildAdapter(mapper.Config).CreateMapExpression<TDst>().ToScript();
        if (logger == null)
        {
            Console.WriteLine(script);
        }
        else
        {
            logger.LogWarning(script);
        }
    }

    public class MapperInfoCard
    {
        public string? SourceType { get; set; }
        public string? DestinationType { get; set; }
        public string? MapExpression { get; set; }
    }

    public static IReadOnlyList<MapperInfoCard> GetInfos()
    {
        var list = new List<MapperInfoCard>();
        TypeAdapterConfig.GlobalSettings.SelfContainedCodeGeneration = true;
        //var def = new ExpressionDefinitions
        //{
        //    IsStatic = true,    //change to false if you want instance
        //    MethodName = "Map",
        //    Namespace = "FlightService.Infrastructure",
        //    TypeName = "CustomerMapper"
        //};
        //from global TypeAdapterConfig get all key value rule map and print
        var map = TypeAdapterConfig.GlobalSettings.RuleMap;
        foreach (var item in map)
        {
            var sourceType = item.Key.Source;
            var destinationType = item.Key.Destination;
            var method = typeof(TypeAdapter).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m is { Name: "BuildAdapter", IsGenericMethod: true }).SingleOrDefault(m =>
                {
                    var parameters = m.GetParameters();
                    return parameters.Length == 1;
                });
            if (method == null)
            {
                throw new InvalidOperationException("BuildAdapter<T> method is not found.");
            }

            var typeAdapterBuilderInstance = (dynamic)method.MakeGenericMethod(sourceType)
                .Invoke(null, new[] { sourceType.GetDefault() })!;
            var createMapExpression = typeof(ITypeAdapterBuilder<>).MakeGenericType(sourceType)
                .GetMethod("CreateMapExpression");
            var expressionResult = createMapExpression!.MakeGenericMethod(destinationType)
                .Invoke(typeAdapterBuilderInstance, null)!;
            var code = (string)ExpressionTranslatorExtensions.ToScript(expressionResult);
            var card = new MapperInfoCard
            {
                SourceType = sourceType.GetCleanFullName(),
                DestinationType = destinationType.GetCleanFullName(),
                MapExpression = code
            };
            list.Add(card);
        }

        return list;
    }
}

internal static class GenericExtensions
{
    public static object? GetDefault(this Type t)
    {
        return typeof(GenericExtensions).GetMethod(nameof(GetDefaultGeneric))!.MakeGenericMethod(t).Invoke(null, null);
    }

    public static T? GetDefaultGeneric<T>()
    {
        return default(T);
    }
}