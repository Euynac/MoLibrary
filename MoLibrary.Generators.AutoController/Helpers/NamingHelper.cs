using System;
using System.Linq;
using MoLibrary.Generators.AutoController.Constants;

namespace MoLibrary.Generators.AutoController.Helpers;

internal static class NamingHelper
{
    /// <summary>
    /// Determines the handler type (Command or Query) from the class name.
    /// </summary>
    /// <param name="className">The class name to analyze</param>
    /// <returns>Handler type or null if not recognized</returns>
    public static string? DetermineHandlerType(string className)
    {
        if (className.Contains(GeneratorConstants.HandlerTypes.CommandHandler))
            return GeneratorConstants.HandlerTypes.Command;
        else if (className.Contains(GeneratorConstants.HandlerTypes.QueryHandler))
            return GeneratorConstants.HandlerTypes.Query;
        else
            return null;
    }

    /// <summary>
    /// Computes the method name by removing handler suffixes from the class name.
    /// </summary>
    /// <param name="className">The class name to process</param>
    /// <returns>The method name</returns>
    public static string ComputeMethodName(string className)
    {
        return className
            .Replace(GeneratorConstants.HandlerTypes.CommandHandler, "")
            .Replace(GeneratorConstants.HandlerTypes.QueryHandler, "");
    }

    /// <summary>
    /// Generates a controller name from the route and handler type.
    /// </summary>
    /// <param name="route">The route path (e.g., "api/v1/Flight")</param>
    /// <param name="handlerType">The handler type (Command or Query)</param>
    /// <returns>The generated controller name</returns>
    public static string GenerateControllerName(string route, string handlerType)
    {
        var segments = route.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
        var lastSegment = segments.LastOrDefault() ?? route;
        var lastSegmentPascalCase = ConvertToPascalCase(lastSegment);

        return $"{GeneratorConstants.Templates.HttpApiControllerPrefix}{handlerType}{lastSegmentPascalCase}";
    }

    /// <summary>
    /// Converts a string to PascalCase, handling underscores and hyphens as word separators.
    /// </summary>
    /// <param name="input">The input string to convert</param>
    /// <returns>The PascalCase string</returns>
    public static string ConvertToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return string.Concat(input
            .Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => char.ToUpper(segment[0]) + segment.Substring(1).ToLower()));
    }

    /// <summary>
    /// Generates a filename for the controller source code.
    /// </summary>
    /// <param name="controllerName">The controller name</param>
    /// <returns>The filename with extension</returns>
    public static string GenerateControllerFileName(string controllerName)
    {
        return $"{controllerName}.Generated.cs";
    }
}