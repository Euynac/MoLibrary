using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MoLibrary.Generators.AutoController.Constants;
using MoLibrary.Generators.AutoController.Helpers;
using MoLibrary.Generators.AutoController.Models;

namespace MoLibrary.Generators.AutoController.Extractors;

internal static class HandlerCandidateExtractor
{
    /// <summary>
    /// Extracts the routing, HTTP method and other required metadata from a candidate class.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to analyze</param>
    /// <param name="compilation">The compilation context</param>
    /// <returns>A HandlerCandidate if extraction is successful, null otherwise</returns>
    public static HandlerCandidate? ExtractHandlerCandidate(ClassDeclarationSyntax classDeclaration, Compilation compilation)
    {
        // Extract and validate the Route attribute
        var routeArg = AttributeHelper.ExtractRouteAttribute(classDeclaration);
        if (routeArg == null) 
            return null;

        // Extract Tags attributes
        var tags = AttributeHelper.ExtractTagsAttributes(classDeclaration);

        var className = classDeclaration.Identifier.Text;

        // Determine handler type based on naming convention
        var handlerType = NamingHelper.DetermineHandlerType(className);
        if (handlerType == null) 
            return null; // Skip if not recognized

        // Compute the method name by removing the handler prefix
        var methodName = NamingHelper.ComputeMethodName(className);

        // Extract the response type from the base class generic arguments
        var responseType = ExtractResponseType(classDeclaration);
        if (responseType == null) 
            return null;

        // Find the method decorated with an HTTP method attribute
        var method = FindHttpMethodDecoratedMethod(classDeclaration);
        if (method == null)
            return null;

        // Retrieve the HTTP method name and its route
        var (httpMethod, httpMethodRoute) = AttributeHelper.ExtractHttpMethodAndRoute(method);
        if (string.IsNullOrEmpty(httpMethod)) 
            return null;

        // Get the request type from the method's parameter
        var requestType = ExtractRequestType(method);
        if (requestType == null) 
            return null;

        // Check if the method parameter has FromForm attribute
        var hasFromFormAttribute = AttributeHelper.HasFromFormAttribute(method);

        // Extract the documentation comment from the class
        var docComment = ExtractDocumentationComment(classDeclaration);

        // Collect original using directives and candidate namespace
        var (originalUsings, candidateNamespace) = ExtractNamespaceInfo(classDeclaration);

        return new HandlerCandidate(
            classRoute: routeArg,
            tags: tags,
            handlerType: handlerType,
            methodName: methodName,
            httpMethodAttribute: httpMethod,
            httpMethodRoute: httpMethodRoute ?? string.Empty,
            requestType: requestType,
            responseType: responseType,
            documentationComment: docComment,
            originalUsings: originalUsings,
            candidateNamespace: candidateNamespace,
            hasFromFormAttribute: hasFromFormAttribute
        );
    }

    /// <summary>
    /// Extracts the response type from the base class generic arguments.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to analyze</param>
    /// <returns>The response type or null if not found</returns>
    private static string? ExtractResponseType(ClassDeclarationSyntax classDeclaration)
    {
        var baseTypeSyntax = classDeclaration.BaseList?.Types.First().Type as GenericNameSyntax;
        if (baseTypeSyntax?.TypeArgumentList.Arguments.Count < 3)
            return null;

        return baseTypeSyntax!.TypeArgumentList.Arguments.Last().ToString();
    }

    /// <summary>
    /// Finds the first method decorated with an HTTP method attribute.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to analyze</param>
    /// <returns>The method declaration or null if not found</returns>
    private static MethodDeclarationSyntax? FindHttpMethodDecoratedMethod(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.Members.OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => AttributeHelper.IsHttpMethodAttribute(a.Name.ToString())));
    }

    /// <summary>
    /// Extracts the request type from the method's first parameter.
    /// </summary>
    /// <param name="method">The method declaration to analyze</param>
    /// <returns>The request type or null if not found</returns>
    private static string? ExtractRequestType(MethodDeclarationSyntax method)
    {
        var requestType = method.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString();
        return string.IsNullOrEmpty(requestType) ? null : requestType;
    }

    /// <summary>
    /// Retrieves and cleans up the documentation comment for a class.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to analyze</param>
    /// <returns>The formatted documentation comment with proper indentation, or empty string if no comment</returns>
    private static string ExtractDocumentationComment(ClassDeclarationSyntax classDeclaration)
    {
        var trivia = classDeclaration.GetLeadingTrivia()
            .FirstOrDefault(tr => tr.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                  tr.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

        if (trivia.IsKind(SyntaxKind.None))
            return string.Empty;

        var rawComment = trivia.ToString();
        if (string.IsNullOrWhiteSpace(rawComment))
            return string.Empty;

        return ProcessDocumentationComment(rawComment);
    }

    /// <summary>
    /// Processes the raw documentation comment to extract only the summary and format it properly.
    /// </summary>
    /// <param name="rawComment">The raw XML documentation comment</param>
    /// <returns>The formatted summary with proper indentation</returns>
    private static string ProcessDocumentationComment(string rawComment)
    {
        // Use regex to extract content between <summary> and </summary> tags
        var summaryMatch = System.Text.RegularExpressions.Regex.Match(
            rawComment, 
            @"<summary>(.*?)</summary>", 
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!summaryMatch.Success)
            return string.Empty;

        // Extract and clean the summary content
        var summaryContent = summaryMatch.Groups[1].Value;
        var lines = summaryContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Replace("///", "").Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => $"        /// {line}");

        if (!lines.Any())
            return string.Empty;

        // Build the complete summary with tags
        return string.Join("\n", 
            ((string[])["        /// <summary>"])
            .Concat(lines)
            .Concat(["        /// </summary>"]));
    }


    /// <summary>
    /// Extracts the original using directives and candidate namespace information.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to analyze</param>
    /// <returns>Tuple containing original using statements and candidate namespace</returns>
    private static (string[] originalUsings, string candidateNamespace) ExtractNamespaceInfo(ClassDeclarationSyntax classDeclaration)
    {
        // Collect original using directives from the candidate's file
        var root = (CompilationUnitSyntax)classDeclaration.SyntaxTree.GetRoot();
        var originalUsings = root.Usings.Select(u => u.Name.ToString()).ToArray();

        // Capture the candidate's own namespace
        var candidateNamespace = string.Empty;
        if (classDeclaration.Parent is BaseNamespaceDeclarationSyntax ns)
        {
            candidateNamespace = ns.Name.ToString();
        }

        return (originalUsings, candidateNamespace);
    }
}