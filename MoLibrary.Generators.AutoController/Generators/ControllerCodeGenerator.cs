using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using MoLibrary.Generators.AutoController.Constants;
using MoLibrary.Generators.AutoController.Diagnostics;
using MoLibrary.Generators.AutoController.Helpers;
using MoLibrary.Generators.AutoController.Models;
using MoLibrary.Generators.AutoController.Templates;

namespace MoLibrary.Generators.AutoController.Generators;

internal static class ControllerCodeGenerator
{
    /// <summary>
    /// Groups candidates by Route and HandlerType, merges tags and using dependencies,
    /// and then generates one controller file per group with comprehensive error handling.
    /// </summary>
    /// <param name="context">The source production context</param>
    /// <param name="candidates">The collection of handler candidates</param>
    public static void GenerateControllers(SourceProductionContext context, List<HandlerCandidate> candidates)
    {
        try
        {
            // Detect route conflicts before generation
            DetectRouteConflicts(context, candidates);

            // Group candidates by the Route (class-level attribute) and handler type
            var groups = candidates.GroupBy(c => (c.ClassRoute, c.HandlerType));

            foreach (var group in groups)
            {
                GenerateControllerForGroup(context, group);
            }
        }
        catch (Exception ex)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.CodeGenerationFailed,
                Location.None,
                "Unknown",
                "Unknown",
                ex.Message);
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// Detects route conflicts between handlers that would result in duplicate endpoints.
    /// </summary>
    /// <param name="context">The source production context for error reporting</param>
    /// <param name="candidates">The collection of handler candidates to check</param>
    private static void DetectRouteConflicts(SourceProductionContext context, List<HandlerCandidate> candidates)
    {
        // Group by final route and HTTP method to detect conflicts
        var routeGroups = candidates
            .GroupBy(c => new {
                Route = BuildFinalRoute(c),
                HttpMethod = c.HttpMethodAttribute
            })
            .Where(g => g.Count() > 1);

        foreach (var conflictGroup in routeGroups)
        {
            var conflictingHandlers = conflictGroup.Select(c => c.MethodName).ToList();
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.RouteConflict,
                Location.None,
                conflictGroup.Key.Route,
                conflictGroup.Key.HttpMethod,
                string.Join(", ", conflictingHandlers));
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// Builds the final route that will be exposed for a handler candidate.
    /// </summary>
    /// <param name="candidate">The handler candidate</param>
    /// <returns>The final route string</returns>
    private static string BuildFinalRoute(HandlerCandidate candidate)
    {
        var baseRoute = candidate.ClassRoute.TrimEnd('/');
        if (string.IsNullOrEmpty(candidate.HttpMethodRoute))
        {
            return baseRoute;
        }
        return $"{baseRoute}/{candidate.HttpMethodRoute.TrimStart('/')}";
    }

    /// <summary>
    /// Generates a single controller for a group of candidates sharing the same route and handler type.
    /// </summary>
    /// <param name="context">The source production context</param>
    /// <param name="group">The group of candidates to generate controller for</param>
    private static void GenerateControllerForGroup(
        SourceProductionContext context,
        IGrouping<(string ClassRoute, string HandlerType), HandlerCandidate> group)
    {
        var route = group.Key.ClassRoute; // e.g., "api/v1/Flight"
        var handlerType = group.Key.HandlerType; // "Command" or "Query"

        try
        {
            // Validate route template format
            if (!IsValidRouteTemplate(route))
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.InvalidRouteTemplate,
                    Location.None,
                    route,
                    $"Group with {group.Count()} handlers");
                context.ReportDiagnostic(diagnostic);
                return;
            }

            // Merge all distinct tags from the group
            var tags = group.SelectMany(c => c.Tags).Distinct().ToList();

            // Generate controller name
            var controllerName = NamingHelper.GenerateControllerName(route, handlerType);

            // Merge using directives
            var allUsings = MergeUsingDirectives(group);
            var usingDirectives = ControllerTemplate.GenerateUsingDirectives(allUsings);

            // Generate all the methods for this controller
            var methodsCode = ControllerTemplate.GenerateAllMethods(group);

            // Compose the complete controller class
            var generatedCode = ControllerTemplate.GenerateControllerClass(
                controllerName,
                route,
                tags,
                usingDirectives,
                methodsCode);

            // Generate the filename and add the source
            var fileName = NamingHelper.GenerateControllerFileName(controllerName);
            context.AddSource(fileName, generatedCode);
        }
        catch (Exception ex)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.CodeGenerationFailed,
                Location.None,
                route,
                handlerType,
                ex.Message);
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// Validates that a route template follows ASP.NET Core routing conventions.
    /// </summary>
    /// <param name="route">The route template to validate</param>
    /// <returns>True if the route is valid, false otherwise</returns>
    private static bool IsValidRouteTemplate(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return false;

        // Basic validation for common route template issues
        if (route.Contains("//") || route.StartsWith("/") || route.EndsWith("/"))
            return false;

        // Check for invalid characters that would break routing
        var invalidChars = new[] { '<', '>', '"', '\'', '\\', '\n', '\r', '\t' };
        if (route.IndexOfAny(invalidChars) >= 0)
            return false;

        return true;
    }

    /// <summary>
    /// Merges using directives from base usings, original usings, and candidate namespaces.
    /// </summary>
    /// <param name="group">The group of candidates</param>
    /// <returns>Collection of merged using statements</returns>
    private static IEnumerable<string> MergeUsingDirectives(
        IGrouping<(string ClassRoute, string HandlerType), HandlerCandidate> group)
    {
        // Include the candidate's own namespaces from where the request/response classes may live
        var candidateNamespaces = group
            .Select(c => c.CandidateNamespace)
            .Where(ns => !string.IsNullOrWhiteSpace(ns));

        // Merge using directives: base usings, the original usings, and candidate namespaces
        var originalUsings = group.SelectMany(c => c.OriginalUsings);
        
        return GeneratorConstants.BaseUsingStatements
            .Concat(originalUsings)
            .Concat(candidateNamespaces);
    }
}