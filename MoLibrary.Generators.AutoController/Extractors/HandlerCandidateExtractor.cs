using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MoLibrary.Generators.AutoController.Constants;
using MoLibrary.Generators.AutoController.Diagnostics;
using MoLibrary.Generators.AutoController.Helpers;
using MoLibrary.Generators.AutoController.Models;

namespace MoLibrary.Generators.AutoController.Extractors;

internal static class HandlerCandidateExtractor
{
    /// <summary>
    /// Extracts the routing, HTTP method and other required metadata from a candidate class with error reporting.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to analyze</param>
    /// <param name="compilation">The compilation context</param>
    /// <param name="config">The generator configuration</param>
    /// <param name="context">The source production context for error reporting</param>
    /// <returns>A HandlerCandidate if extraction is successful, null otherwise</returns>
    public static HandlerCandidate? ExtractHandlerCandidate(ClassDeclarationSyntax classDeclaration, Compilation compilation, Models.GeneratorConfig config, SourceProductionContext context)
    {
        var className = classDeclaration.Identifier.Text;
        var location = classDeclaration.GetLocation();

        try
        {
            // Validate ApplicationService inheritance
            if (!ValidateApplicationServiceInheritance(classDeclaration, context))
                return null;

            // Extract and validate the Route attribute (with fallback to configuration)
            var routeArg = AttributeHelper.ExtractRouteAttribute(classDeclaration, config);
            if (routeArg == null)
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.MissingConfigurationAttribute,
                    location,
                    className);
                context.ReportDiagnostic(diagnostic);
                return null;
            }

            // Extract Tags attributes
            var tags = AttributeHelper.ExtractTagsAttributes(classDeclaration);

            // Determine handler type based on naming convention
            var handlerType = NamingHelper.DetermineHandlerType(className);
            if (handlerType == null)
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.UnknownHandlerType,
                    location,
                    className);
                context.ReportDiagnostic(diagnostic);
                return null;
            }

            // Compute the method name by removing the handler prefix
            var methodName = NamingHelper.ComputeMethodName(className);

            // Extract the response type from the base class generic arguments
            var responseType = ExtractResponseType(classDeclaration);
            if (responseType == null)
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.InvalidApplicationServiceInheritance,
                    location,
                    className);
                context.ReportDiagnostic(diagnostic);
                return null;
            }

            // Find the method (either decorated with an HTTP method attribute or any public method for CQRS)
            var method = FindHttpMethodDecoratedMethod(classDeclaration) ?? FindPublicMethod(classDeclaration);
            if (method == null)
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.MissingHandleMethod,
                    location,
                    className);
                context.ReportDiagnostic(diagnostic);
                return null;
            }

            // Validate method signature
            if (!ValidateMethodSignature(method, context))
                return null;

            // Retrieve the HTTP method name and its route (supports CQRS fallback)
            var (httpMethod, httpMethodRoute) = AttributeHelper.ExtractHttpMethodAndRoute(method, className);
            if (string.IsNullOrEmpty(httpMethod))
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.MissingHttpMethodAttribute,
                    method.GetLocation(),
                    method.Identifier.Text,
                    className);
                context.ReportDiagnostic(diagnostic);
                return null;
            }

            // Get the request type from the method's parameter
            var requestType = ExtractRequestType(method);
            if (requestType == null)
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.InvalidHandleMethodSignature,
                    method.GetLocation(),
                    className);
                context.ReportDiagnostic(diagnostic);
                return null;
            }

            // Check if the method parameter has FromForm attribute
            var hasFromFormAttribute = AttributeHelper.HasFromFormAttribute(method);

            // Extract the documentation comment and plain text summary from the class
            var (docComment, plainTextSummary) = ExtractDocumentationCommentAndSummary(classDeclaration);

            // Collect original using directives and candidate namespace
            var (originalUsings, candidateNamespace) = ExtractNamespaceInfo(classDeclaration);

            // Get semantic model for namespace extraction
            var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);

            // Extract related namespaces using semantic analysis
            var relatedNamespacesArray = ExtractRelatedNamespaces(classDeclaration, method, semanticModel);
            var relatedNamespaces = new HashSet<string>(relatedNamespacesArray);

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
                hasFromFormAttribute: hasFromFormAttribute,
                relatedNamespaces: relatedNamespaces,
                plainTextSummary: plainTextSummary
            );
        }
        catch (Exception ex)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.HandlerExtractionFailed,
                location,
                className,
                ex.Message);
            context.ReportDiagnostic(diagnostic);
            return null;
        }
    }

    /// <summary>
    /// Extracts the routing, HTTP method and other required metadata from a candidate class (compatibility overload).
    /// </summary>
    /// <param name="classDeclaration">The class declaration to analyze</param>
    /// <param name="compilation">The compilation context</param>
    /// <param name="config">The generator configuration</param>
    /// <returns>A HandlerCandidate if extraction is successful, null otherwise</returns>
    public static HandlerCandidate? ExtractHandlerCandidate(ClassDeclarationSyntax classDeclaration, Compilation compilation, Models.GeneratorConfig config)
    {
        // Extract and validate the Route attribute (with fallback to configuration)
        var routeArg = AttributeHelper.ExtractRouteAttribute(classDeclaration, config);
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

        // Find the method (either decorated with an HTTP method attribute or any public method for CQRS)
        var method = FindHttpMethodDecoratedMethod(classDeclaration) ?? FindPublicMethod(classDeclaration);
        if (method == null)
            return null;

        // Retrieve the HTTP method name and its route (supports CQRS fallback)
        var (httpMethod, httpMethodRoute) = AttributeHelper.ExtractHttpMethodAndRoute(method, className);
        if (string.IsNullOrEmpty(httpMethod))
            return null;

        // Get the request type from the method's parameter
        var requestType = ExtractRequestType(method);
        if (requestType == null)
            return null;

        // Check if the method parameter has FromForm attribute
        var hasFromFormAttribute = AttributeHelper.HasFromFormAttribute(method);

        // Extract the documentation comment and plain text summary from the class
        var (docComment, plainTextSummary) = ExtractDocumentationCommentAndSummary(classDeclaration);

        // Collect original using directives and candidate namespace
        var (originalUsings, candidateNamespace) = ExtractNamespaceInfo(classDeclaration);

        // Get semantic model for namespace extraction
        var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);

        // Extract related namespaces using semantic analysis
        var relatedNamespacesArray = ExtractRelatedNamespaces(classDeclaration, method, semanticModel);
        var relatedNamespaces = new HashSet<string>(relatedNamespacesArray);

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
            hasFromFormAttribute: hasFromFormAttribute,
            relatedNamespaces: relatedNamespaces,
            plainTextSummary: plainTextSummary
        );
    }

    /// <summary>
    /// Validates that the class properly inherits from ApplicationService with correct generic arguments.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to validate</param>
    /// <param name="context">The source production context for error reporting</param>
    /// <returns>True if inheritance is valid, false otherwise</returns>
    private static bool ValidateApplicationServiceInheritance(ClassDeclarationSyntax classDeclaration, SourceProductionContext context)
    {
        var baseTypeSyntax = classDeclaration.BaseList?.Types.First().Type as GenericNameSyntax;
        if (baseTypeSyntax?.TypeArgumentList.Arguments.Count < 2)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.InvalidApplicationServiceInheritance,
                classDeclaration.GetLocation(),
                classDeclaration.Identifier.Text);
            context.ReportDiagnostic(diagnostic);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Validates that the method has a correct signature for a handler method.
    /// </summary>
    /// <param name="method">The method declaration to validate</param>
    /// <param name="context">The source production context for error reporting</param>
    /// <returns>True if signature is valid, false otherwise</returns>
    private static bool ValidateMethodSignature(MethodDeclarationSyntax method, SourceProductionContext context)
    {
        // Check if method has at least one parameter
        if (method.ParameterList.Parameters.Count == 0)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.InvalidHandleMethodSignature,
                method.GetLocation(),
                "Missing request parameter");
            context.ReportDiagnostic(diagnostic);
            return false;
        }

        // Check if return type looks like Task<T>
        var returnTypeString = method.ReturnType.ToString();
        if (!returnTypeString.StartsWith("Task<") && !returnTypeString.StartsWith("async Task<"))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.InvalidHandleMethodSignature,
                method.GetLocation(),
                $"Invalid return type '{returnTypeString}'. Expected Task<TResponse>");
            context.ReportDiagnostic(diagnostic);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Extracts the response type from the base class generic arguments.
    /// Returns simple type name without namespace for concise metadata.
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
    /// Finds any public method for CQRS convention fallback.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to analyze</param>
    /// <returns>The first public method declaration or null if not found</returns>
    private static MethodDeclarationSyntax? FindPublicMethod(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.Members.OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)));
    }

    /// <summary>
    /// Extracts the request type from the method's first parameter.
    /// Returns simple type name without namespace for concise metadata.
    /// </summary>
    /// <param name="method">The method declaration to analyze</param>
    /// <returns>The request type or null if not found</returns>
    private static string? ExtractRequestType(MethodDeclarationSyntax method)
    {
        var requestType = method.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString();
        return string.IsNullOrEmpty(requestType) ? null : requestType;
    }

    /// <summary>
    /// Retrieves and cleans up the documentation comment for a class, returning both formatted and plain text versions.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to analyze</param>
    /// <returns>Tuple of (formatted documentation comment, plain text summary)</returns>
    private static (string formattedComment, string plainTextSummary) ExtractDocumentationCommentAndSummary(ClassDeclarationSyntax classDeclaration)
    {
        var trivia = classDeclaration.GetLeadingTrivia()
            .FirstOrDefault(tr => tr.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                  tr.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

        if (trivia.IsKind(SyntaxKind.None))
            return (string.Empty, string.Empty);

        var rawComment = trivia.ToString();
        if (string.IsNullOrWhiteSpace(rawComment))
            return (string.Empty, string.Empty);

        // Use regex to extract content between <summary> and </summary> tags
        var summaryMatch = System.Text.RegularExpressions.Regex.Match(
            rawComment,
            @"<summary>(.*?)</summary>",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!summaryMatch.Success)
            return (string.Empty, string.Empty);

        // Extract and clean the summary content
        var summaryContent = summaryMatch.Groups[1].Value;
        var cleanLines = summaryContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Replace("///", "").Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (!cleanLines.Any())
            return (string.Empty, string.Empty);

        // Generate formatted comment with proper indentation
        var formattedLines = cleanLines.Select(line => $"        /// {line}").ToList();
        var formattedComment = string.Join("\n",
            ((string[])["        /// <summary>"])
            .Concat(formattedLines)
            .Concat(["        /// </summary>"]));

        // Generate plain text summary (without XML tags or ///)
        var plainTextSummary = string.Join(" ", cleanLines).Trim();

        return (formattedComment, plainTextSummary);
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
        var originalUsings = root.Usings.Select(u =>
        {
            // Handle using alias: using Alias = Namespace.Type;
            if (u.Alias != null)
            {
                return $"{u.Alias} {u.Name}";
            }
            // Handle using static: using static Namespace.Type;
            if (u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
            {
                return $"static {u.Name}";
            }
            // Handle normal using: using Namespace;
            return u.Name.ToString();
        }).ToArray();

        // Capture the candidate's own namespace
        var candidateNamespace = string.Empty;
        if (classDeclaration.Parent is BaseNamespaceDeclarationSyntax ns)
        {
            candidateNamespace = ns.Name.ToString();
        }

        return (originalUsings, candidateNamespace);
    }

    /// <summary>
    /// Extracts all unique namespaces from request and response types using SemanticModel.
    /// Handles generic types by extracting namespaces from both the outer type and generic arguments.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to analyze</param>
    /// <param name="method">The method declaration to analyze</param>
    /// <param name="semanticModel">The semantic model for type resolution</param>
    /// <returns>Array of unique, sorted namespaces</returns>
    private static string[] ExtractRelatedNamespaces(
        ClassDeclarationSyntax classDeclaration,
        MethodDeclarationSyntax method,
        SemanticModel semanticModel)
    {
        var namespaces = new HashSet<string>();

        // Extract namespaces from response type
        var baseTypeSyntax = classDeclaration.BaseList?.Types.First().Type as GenericNameSyntax;
        if (baseTypeSyntax?.TypeArgumentList.Arguments.Count >= 3)
        {
            var responseTypeSyntax = baseTypeSyntax.TypeArgumentList.Arguments.Last();
            CollectNamespacesFromType(responseTypeSyntax, semanticModel, namespaces);
        }

        // Extract namespaces from request type
        var firstParameter = method.ParameterList.Parameters.FirstOrDefault();
        if (firstParameter?.Type != null)
        {
            CollectNamespacesFromType(firstParameter.Type, semanticModel, namespaces);
        }

        return namespaces
            .Where(ns => !string.IsNullOrEmpty(ns) && !ns.StartsWith("System"))
            .OrderBy(ns => ns)
            .ToArray();
    }

    /// <summary>
    /// Collects namespaces from a type syntax, including generic type arguments.
    /// </summary>
    /// <param name="typeSyntax">The type syntax to analyze</param>
    /// <param name="semanticModel">The semantic model for type resolution</param>
    /// <param name="namespaces">HashSet to collect unique namespaces</param>
    private static void CollectNamespacesFromType(
        TypeSyntax typeSyntax,
        SemanticModel semanticModel,
        HashSet<string> namespaces)
    {
        var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
        var typeSymbol = typeInfo.Type;

        if (typeSymbol == null) return;

        // Handle generic types (e.g., Res<ResponseType>)
        if (typeSymbol is INamedTypeSymbol namedType)
        {
            // Add the namespace of the outer type
            var ns = namedType.ContainingNamespace?.ToDisplayString();
            if (!string.IsNullOrEmpty(ns))
                namespaces.Add(ns);

            // Process generic type arguments recursively
            if (namedType.IsGenericType)
            {
                foreach (var typeArg in namedType.TypeArguments)
                {
                    CollectNamespacesFromTypeSymbol(typeArg, namespaces);
                }
            }
        }
        else
        {
            var ns = typeSymbol.ContainingNamespace?.ToDisplayString();
            if (!string.IsNullOrEmpty(ns))
                namespaces.Add(ns);
        }
    }

    /// <summary>
    /// Recursively collects namespaces from a type symbol.
    /// </summary>
    private static void CollectNamespacesFromTypeSymbol(ITypeSymbol typeSymbol, HashSet<string> namespaces)
    {
        if (typeSymbol == null) return;

        var ns = typeSymbol.ContainingNamespace?.ToDisplayString();
        if (!string.IsNullOrEmpty(ns))
            namespaces.Add(ns);

        // Handle nested generic types
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            foreach (var typeArg in namedType.TypeArguments)
            {
                CollectNamespacesFromTypeSymbol(typeArg, namespaces);
            }
        }
    }
}