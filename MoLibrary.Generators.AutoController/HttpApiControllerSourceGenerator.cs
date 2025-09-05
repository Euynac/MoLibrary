using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
// For AddSource

namespace MoLibrary.Generators.AutoController;

//https://www.cnblogs.com/fanshaoO/p/18101185
//巨坑：第一次生成后后续重新构建项目不再生成，需要重启VS才会使用最新的Generator
//https://stackoverflow.com/questions/76891987/must-restart-visual-studio-for-source-generator-files-to-be-picked-up
[Generator]
public class HttpApiControllerSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        //Debugger.Launch();
        // Filter classes that derive from an application service.
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax { BaseList: { } },
                transform: static (ctx, _) => (ClassDeclarationSyntax) ctx.Node)
            .Where(static c => c.BaseList!.Types.Any(t => t.ToString().Contains("ApplicationService")));

        // Transform each candidate class into a HandlerCandidate.
        var handlerCandidates = classDeclarations
            .Combine(context.CompilationProvider)
            .Select((pair, token) => ExtractHandlerCandidate(pair.Left, pair.Right))
            .Where(candidate => candidate is not null)
            .Collect();


        // After processing all candidates, group them by Route and HandlerType.
        context.RegisterSourceOutput(handlerCandidates, (spc, candidates) =>
        {
            GenerateControllers(spc, candidates.Where(c => c is not null).Select(c => c!).ToList());
        });
    }

    /// <summary>
    /// Extracts the routing, HTTP method and other required metadata from a candidate class.
    /// </summary>
    private static HandlerCandidate? ExtractHandlerCandidate(ClassDeclarationSyntax cls, Compilation compilation)
    {
        // Retrieve the [Route] attribute.
        var routeAttribute = cls.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString() == "Route");
        if (routeAttribute == null) return null;
        var routeArg = routeAttribute.ArgumentList?.Arguments.FirstOrDefault()?.ToString()?.Trim('"');
        if (string.IsNullOrEmpty(routeArg)) return null;

        // Gather any [Tags] attributes from the class.
        var tags = cls.AttributeLists
            .SelectMany(al => al.Attributes)
            .Where(attr => attr.Name.ToString() == "Tags")
            .Select(attr => attr.ArgumentList?.Arguments.FirstOrDefault()?.ToString()?.Trim('"'))
            .Where(tag => !string.IsNullOrEmpty(tag))
            .Distinct()
            .ToList();

        var className = cls.Identifier.Text;

        // Determine handler type based on naming convention.
        string handlerType;
        if (className.Contains("CommandHandler"))
            handlerType = "Command";
        else if (className.Contains("QueryHandler"))
            handlerType = "Query";
        else
            return null; // Skip if not recognized.

        // Compute the method name by removing the handler prefix.
        var methodName = className.Replace("CommandHandler", "").Replace("QueryHandler", "");

        // Extract the response type from the base class generic arguments.
        var baseTypeSyntax = cls.BaseList!.Types.First().Type as GenericNameSyntax;
        if (baseTypeSyntax == null || baseTypeSyntax.TypeArgumentList.Arguments.Count < 3)
            return null;
        var responseType = baseTypeSyntax.TypeArgumentList.Arguments.Last().ToString();

        // Find the method decorated with an HTTP method attribute.
        var method = cls.Members.OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => IsHttpMethodAttribute(a.Name.ToString())));
        if (method == null)
            return null;

        // Retrieve the HTTP method name and its route.
        var (httpMethod, httpMethodRoute) = GetHttpMethodAndRoute(method);
        if (string.IsNullOrEmpty(httpMethod)) return null;

        // Get the request type from the method's parameter.
        var requestType = method.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString();
        if (string.IsNullOrEmpty(requestType)) return null;

        // Extract the documentation comment from the class instead of the method.
        var docComment = GetDocumentationComment(cls);

        // Collect original using directives from the candidate's file.
        var root = (CompilationUnitSyntax) cls.SyntaxTree.GetRoot();
        var originalUsings = root.Usings.Select(u => u.Name.ToString());

        // Also, capture the candidate's own namespace.
        var candidateNamespace = string.Empty;
        if (cls.Parent is BaseNamespaceDeclarationSyntax ns)
        {
            candidateNamespace = ns.Name.ToString();
        }

        return new HandlerCandidate(
            classRoute: routeArg!,
            tags: tags,
            handlerType: handlerType,
            methodName: methodName,
            httpMethodAttribute: httpMethod,
            httpMethodRoute: httpMethodRoute ?? string.Empty,
            requestType: requestType!,
            responseType: responseType,
            documentationComment: docComment,
            originalUsings: originalUsings,
            candidateNamespace: candidateNamespace
        );
    }

    /// <summary>
    /// Extracts the HTTP method attribute (e.g. HttpPost) and its associated route from a method.
    /// </summary>
    private static (string httpMethod, string? route) GetHttpMethodAndRoute(MethodDeclarationSyntax method)
    {
        var attr = method.AttributeLists
                  .SelectMany(al => al.Attributes)
                  .FirstOrDefault(a => IsHttpMethodAttribute(a.Name.ToString()));
        if (attr == null)
            return (string.Empty, null);
        var httpMethod = attr.Name.ToString();
        var routeArgument = attr.ArgumentList?.Arguments.FirstOrDefault()?.ToString()?.Trim('"');
        return (httpMethod, routeArgument);
    }

    private static bool IsHttpMethodAttribute(string attributeName)
    {
        return attributeName switch
        {
            "HttpPost" or "HttpPut" or "HttpGet" or "HttpDelete" or "HttpPatch" => true,
            _ => false
        };
    }

    /// <summary>
    /// Retrieves and cleans up the documentation comment for a class.
    /// </summary>
    private static string GetDocumentationComment(ClassDeclarationSyntax cls)
    {
        var trivia = cls.GetLeadingTrivia()
                   .FirstOrDefault(tr => tr.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                         tr.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        var comment = $"///{trivia.ToString()}";
        //if (!string.IsNullOrWhiteSpace(comment))
        //{
        //    comment = comment.Replace("/**", "").Replace("*/", "").Replace("*", "").Trim();
        //}
        return comment;
    }

    /// <summary>
    /// Groups candidates by Route and HandlerType, unions tags and using dependencies, and then generates one controller file per group.
    /// Auto-formats the generated code using Roslyn's Formatter.
    /// </summary>
    private static void GenerateControllers(SourceProductionContext context, List<HandlerCandidate> candidates)
    {
        // Group candidates by the Route (class-level attribute) and handler type.
        var groups = candidates.GroupBy(c => (c.ClassRoute, c.HandlerType));

        foreach (var group in groups)
        {
            var route = group.Key.ClassRoute; // e.g., "api/v1/Flight"
            var handlerType = group.Key.HandlerType; // "Command" or "Query"
            // Merge all distinct tags from the group.
            var tags = group.SelectMany(c => c.Tags).Distinct().ToList();

            // Compute the controller name by taking the last segment of the route.
            var segments = route.Split(['/'], System.StringSplitOptions.RemoveEmptyEntries);
            var lastSegment = segments.LastOrDefault() ?? route;
            var lastSegmentPascalCase = string.Concat(lastSegment
                .Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => char.ToUpper(segment[0]) + segment.Substring(1).ToLower()));

            var controllerName = $"HttpApi{handlerType}{lastSegmentPascalCase}";
            if (segments.ElementAtOrDefault(0) == "api" && segments.ElementAtOrDefault(1) is { } version && version.StartsWith("v"))
            {
                controllerName = $"{controllerName}V{version.Substring(1, version.Length - 1)}";
            }

            // Merge using directives: base usings plus all using directives from the candidates.
            var baseUsings = new[]
        {
                "Microsoft.AspNetCore.Mvc",
                "System.Net",
                "System.Threading.Tasks",
                "MediatR",
                "MoLibrary.Core.Extensions",
                "MoLibrary.Tool.Extensions"
            };

            // Include the candidate's own namespaces from where the request/response classes may live.
            var candidateNamespaces = group
                .Select(c => c.CandidateNamespace)
                .Where(ns => !string.IsNullOrWhiteSpace(ns));

            // Merge using directives: base usings, the original usings, and candidate namespaces.
            var originalUsings = group.SelectMany(c => c.OriginalUsings);
            var allUsings = baseUsings
                                .Concat(originalUsings)
                                .Concat(candidateNamespaces)
                                .Distinct()
                                .OrderBy(u => u)
                                .Select(u => $"using {u};");
            var usingDirectives = string.Join("\n", allUsings);

            // Generate all the methods for this controller.
            var methodsCode = "";
            //TODO 返回值是否影响ActionResult？
            foreach (var candidate in group)
            {
                methodsCode += $$"""
                    {{candidate.DocumentationComment}}
                    [{{candidate.HttpMethodAttribute}}("{{candidate.HttpMethodRoute}}")]
                    [ProducesResponseType((int) HttpStatusCode.Accepted)]
                    [ProducesResponseType((int) HttpStatusCode.BadRequest)]
                    [ProducesResponseType(typeof(Res<{{candidate.ResponseType}}>), (int) HttpStatusCode.OK)]
                    public async Task<object> {{candidate.MethodName}}(
                        [{{(candidate.HttpMethodAttribute == "HttpGet" ? "FromQuery" : "FromBody")}}] {{candidate.RequestType}} dto)
                    {
                        return await mediator.Send(dto).GetResponse(this);
                    }
                    
                    """;
            }

            var tagAttributeContent = "";
            if (string.Join(", ", tags.Select(tag => $"\"{tag}\"")) is { } tagContent &&
                !string.IsNullOrEmpty(tagContent))
            {
                tagAttributeContent = $"\n[Tags({tagContent})]";
            }

            // Compose the complete controller class using the merged usings and methods.
            var generatedCode = $$"""
            // <auto-generated/>
            {{usingDirectives}}

            namespace GeneratedControllers
            {
                [Route("{{route}}")]
                [ApiController]{{tagAttributeContent}}
                public class {{controllerName}}(IMediator mediator) : ControllerBase
                {
                    {{methodsCode}}
                }
            }
            """;

            context.AddSource($"{controllerName}.Generated.cs", generatedCode);


            // Auto-format the generated code using Roslyn's Formatter.
            //var syntaxTree = CSharpSyntaxTree.ParseText(generatedCode);
            //var root = syntaxTree.GetRoot();
            //var workspace = new AdhocWorkspace();
            //var formattedRoot = Formatter.Format(root, workspace);
            //var formattedCode = formattedRoot.ToFullString();

            //context.AddSource($"{controllerName}.Generated.cs", SourceText.From(formattedCode, System.Text.Encoding.UTF8));
        }
    }
}

/// <summary>
/// A record to store all the relevant info for a candidate handler.
/// </summary>
internal class HandlerCandidate
{
    public string ClassRoute { get; }
    public List<string> Tags { get; }
    public string HandlerType { get; }
    public string MethodName { get; }
    public string HttpMethodAttribute { get; }
    public string HttpMethodRoute { get; }
    public string RequestType { get; }
    public string ResponseType { get; }
    public string DocumentationComment { get; }
    public IEnumerable<string> OriginalUsings { get; }
    public string CandidateNamespace { get; }

    /// <summary>
    /// A record to store all the relevant info for a candidate handler.
    /// </summary>
    public HandlerCandidate(string classRoute,
        List<string> tags,
        string handlerType,
        string methodName,
        string httpMethodAttribute,
        string httpMethodRoute,
        string requestType,
        string responseType,
        string documentationComment,
        IEnumerable<string> originalUsings, string candidateNamespace)
    {
        ClassRoute = classRoute;
        Tags = tags;
        HandlerType = handlerType;
        MethodName = methodName;
        HttpMethodAttribute = httpMethodAttribute;
        HttpMethodRoute = httpMethodRoute;
        RequestType = requestType;
        ResponseType = responseType;
        DocumentationComment = documentationComment;
        OriginalUsings = originalUsings;
        CandidateNamespace = candidateNamespace;
    }
}
