using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MoLibrary.Generators.AutoController.Constants;
using MoLibrary.Generators.AutoController.Models;

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
        // Check for full handler names (e.g., "CommandHandler", "QueryHandler")
        if (className.Contains(GeneratorConstants.HandlerTypes.CommandHandler))
            return GeneratorConstants.HandlerTypes.Command;
        else if (className.Contains(GeneratorConstants.HandlerTypes.QueryHandler))
            return GeneratorConstants.HandlerTypes.Query;
        // Check for CQRS style names (e.g., "CommandCreateUser", "QueryGetUser")
        else if (className.StartsWith(GeneratorConstants.HandlerTypes.Command))
            return GeneratorConstants.HandlerTypes.Command;
        else if (className.StartsWith(GeneratorConstants.HandlerTypes.Query))
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

    /// <summary>
    /// Generates a default route based on the configuration.
    /// Pattern: {DefaultRoutePrefix}/{DomainName} if DomainName exists, otherwise just {DefaultRoutePrefix}
    /// </summary>
    /// <param name="classDeclaration">The class declaration</param>
    /// <param name="config">The generator configuration</param>
    /// <returns>The generated route or null if generation is not possible</returns>
    public static string? GenerateDefaultRoute(ClassDeclarationSyntax classDeclaration, GeneratorConfig config)
    {
        if (!config.HasDefaultRouting)
            return null;

        if (string.IsNullOrEmpty(config.DefaultRoutePrefix))
            return null;

        var routePrefix = config.DefaultRoutePrefix!.TrimEnd('/');

        // Pattern: {RoutePrefix}/{DomainName} if DomainName exists, otherwise just {RoutePrefix}
        if (!string.IsNullOrEmpty(config.DomainName))
        {
            return $"{routePrefix}/{config.DomainName}";
        }

        // Just use the route prefix
        return routePrefix;
    }

    /// <summary>
    /// Extracts the service name from a class name by removing common suffixes.
    /// </summary>
    /// <param name="className">The class name to process</param>
    /// <returns>The extracted service name</returns>
    public static string ExtractServiceName(string className)
    {
        var serviceName = className
            .Replace(GeneratorConstants.HandlerTypes.CommandHandler, "")
            .Replace(GeneratorConstants.HandlerTypes.QueryHandler, "")
            .Replace("Handler", "")
            .Replace("Service", "")
            .Replace("AppService", "");

        return ConvertToPascalCase(serviceName);
    }

    /// <summary>
    /// Extracts method name from CQRS handler class name and converts to kebab-case.
    /// Example: QueryGetUserName -> get-user-name, CommandCreateUser -> create-user
    /// </summary>
    /// <param name="className">The handler class name</param>
    /// <returns>The method name in kebab-case format</returns>
    public static string ExtractCqrsMethodName(string className)
    {
        var methodName = className;

        // Remove Query/Command prefixes
        if (methodName.StartsWith("Query"))
            methodName = methodName.Substring(5);
        else if (methodName.StartsWith("Command"))
            methodName = methodName.Substring(7);
        // Remove Handler prefix
        if (methodName.StartsWith("Handler"))
            methodName = methodName.Substring(7);
        // Remove Handler suffix
        else if (methodName.EndsWith("Handler"))
            methodName = methodName.Substring(0, methodName.Length - 7);

        // Convert to kebab-case
        return ConvertToKebabCase(methodName);
    }

    /// <summary>
    /// Converts a PascalCase string to kebab-case.
    /// Example: GetUserName -> get-user-name
    /// </summary>
    /// <param name="input">The PascalCase input string</param>
    /// <returns>The kebab-case string</returns>
    public static string ConvertToKebabCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = "";
        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c) && i > 0)
            {
                result += "-";
            }
            result += char.ToLower(c);
        }

        return result;
    }

    /// <summary>
    /// Determines the default HTTP method based on CQRS handler type.
    /// Query handlers default to GET, Command handlers default to POST.
    /// </summary>
    /// <param name="className">The handler class name</param>
    /// <returns>The default HTTP method (HttpGet or HttpPost)</returns>
    public static string GetDefaultHttpMethod(string className)
    {
        var handlerType = DetermineHandlerType(className);
        return handlerType == GeneratorConstants.HandlerTypes.Query ? "HttpGet" : "HttpPost";
    }

}