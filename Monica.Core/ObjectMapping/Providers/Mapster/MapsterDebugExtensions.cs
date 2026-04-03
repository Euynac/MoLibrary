using ExpressionDebugger;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace Monica.Core.Features.MoMapper;

/// <summary>
/// Debug helpers for inspecting Mapster-generated mapping expressions.
/// </summary>
public static class MapsterDebugExtensions
{
    /// <summary>
    /// Writes the generated mapping expression for the provided source value.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="source">The source value used to build the mapping expression.</param>
    /// <param name="logger">Optional logger used instead of the console.</param>
    public static void MapDebug<TSource, TDestination>(this IMapper mapper, TSource source, ILogger? logger = null)
    {
        var script = source.BuildAdapter(mapper.Config).CreateMapExpression<TDestination>().ToScript();
        if (logger == null)
        {
            Console.WriteLine(script);
            return;
        }

        logger.LogWarning(script);
    }
}
