using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using MoLibrary.Generators.AutoController.Constants;
using MoLibrary.Generators.AutoController.Helpers;
using MoLibrary.Generators.AutoController.Models;
using MoLibrary.Generators.AutoController.Templates;

namespace MoLibrary.Generators.AutoController.Generators;

internal static class ControllerCodeGenerator
{
    /// <summary>
    /// Groups candidates by Route and HandlerType, merges tags and using dependencies, 
    /// and then generates one controller file per group.
    /// </summary>
    /// <param name="context">The source production context</param>
    /// <param name="candidates">The collection of handler candidates</param>
    public static void GenerateControllers(SourceProductionContext context, List<HandlerCandidate> candidates)
    {
        // Group candidates by the Route (class-level attribute) and handler type
        var groups = candidates.GroupBy(c => (c.ClassRoute, c.HandlerType));

        foreach (var group in groups)
        {
            GenerateControllerForGroup(context, group);
        }
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