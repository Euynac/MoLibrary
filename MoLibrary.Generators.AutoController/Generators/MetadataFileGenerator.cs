using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using MoLibrary.Generators.AutoController.Helpers;
using MoLibrary.Generators.AutoController.Models;

namespace MoLibrary.Generators.AutoController.Generators;

/// <summary>
/// Generates RPC metadata files for client code generation.
/// Since Source Generators cannot write directly to the file system,
/// we generate a C# file containing the JSON metadata as a comment,
/// which will be extracted by MSBuild post-processing.
/// </summary>
internal static class MetadataFileGenerator
{
    /// <summary>
    /// Generates RPC metadata as a C# file containing JSON in comments.
    /// The JSON will be extracted by MSBuild to create the actual .rpc-metadata.json file.
    /// </summary>
    /// <param name="context">The source production context</param>
    /// <param name="candidates">The handler candidates to convert to metadata</param>
    /// <param name="config">The generator configuration</param>
    /// <param name="compilation">The compilation context</param>
    public static void GenerateMetadataFile(
        SourceProductionContext context,
        List<HandlerCandidate> candidates,
        GeneratorConfig config,
        Compilation compilation)
    {
        if (candidates == null || candidates.Count == 0)
            return;

        // Convert candidates to metadata
        var metadata = ConvertToMetadata(candidates, config, compilation);

        // Serialize to JSON
        var json = SerializeToJson(metadata);

        // Generate C# file containing the JSON as a specially-marked comment
        var sourceCode = GenerateMetadataSourceFile(json);

        // Add the source file
        context.AddSource("__RpcMetadata.g.cs", sourceCode);
    }

    /// <summary>
    /// Converts handler candidates to RPC metadata.
    /// </summary>
    private static RpcMetadata ConvertToMetadata(
        List<HandlerCandidate> candidates,
        GeneratorConfig config,
        Compilation compilation)
    {
        var metadata = new RpcMetadata
        {
            AssemblyName = compilation.AssemblyName ?? "Unknown",
            DomainName = config.DomainName,
            RoutePrefix = config.DefaultRoutePrefix,
            Handlers = new List<HandlerMetadata>()
        };

        foreach (var candidate in candidates)
        {
            var handlerMetadata = new HandlerMetadata
            {
                HandlerName = ExtractSimpleHandlerName(candidate.HandlerType),
                FullTypeName = $"{candidate.CandidateNamespace}.{candidate.HandlerType}",
                RequestType = candidate.RequestType,
                ResponseType = candidate.ResponseType,
                HttpMethod = NormalizeHttpMethod(candidate.HttpMethodAttribute),
                Route = BuildCompleteRoute(candidate),
                Namespace = candidate.CandidateNamespace,
                Tags = candidate.Tags,
                ClientMethodName = candidate.MethodName,
                HandlerType = NamingHelper.DetermineHandlerType(candidate.HandlerType) ?? "Unknown"
            };

            metadata.Handlers.Add(handlerMetadata);
        }

        return metadata;
    }

    /// <summary>
    /// Builds the complete route by combining class route and method route.
    /// </summary>
    private static string BuildCompleteRoute(HandlerCandidate candidate)
    {
        var baseRoute = candidate.ClassRoute.TrimEnd('/');
        if (string.IsNullOrEmpty(candidate.HttpMethodRoute))
        {
            return baseRoute;
        }
        return $"{baseRoute}/{candidate.HttpMethodRoute.TrimStart('/')}";
    }

    /// <summary>
    /// Extracts the simple handler name from the full type name.
    /// </summary>
    private static string ExtractSimpleHandlerName(string fullTypeName)
    {
        var lastDot = fullTypeName.LastIndexOf('.');
        return lastDot >= 0 ? fullTypeName.Substring(lastDot + 1) : fullTypeName;
    }

    /// <summary>
    /// Normalizes HTTP method attribute name to standard HTTP method.
    /// </summary>
    private static string NormalizeHttpMethod(string httpMethodAttribute)
    {
        // HttpPost -> POST, HttpGet -> GET, etc.
        return httpMethodAttribute.Replace("Http", "").ToUpperInvariant();
    }

    /// <summary>
    /// Serializes metadata to JSON string.
    /// Using manual JSON generation to avoid dependencies (Source Generators run in limited environment).
    /// </summary>
    private static string SerializeToJson(RpcMetadata metadata)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"AssemblyName\": \"{EscapeJson(metadata.AssemblyName)}\",");
        sb.AppendLine($"  \"DomainName\": {JsonValue(metadata.DomainName)},");
        sb.AppendLine($"  \"RoutePrefix\": {JsonValue(metadata.RoutePrefix)},");
        sb.AppendLine("  \"Handlers\": [");

        for (int i = 0; i < metadata.Handlers.Count; i++)
        {
            var handler = metadata.Handlers[i];
            sb.AppendLine("    {");
            sb.AppendLine($"      \"HandlerName\": \"{EscapeJson(handler.HandlerName)}\",");
            sb.AppendLine($"      \"FullTypeName\": \"{EscapeJson(handler.FullTypeName)}\",");
            sb.AppendLine($"      \"RequestType\": \"{EscapeJson(handler.RequestType)}\",");
            sb.AppendLine($"      \"ResponseType\": \"{EscapeJson(handler.ResponseType)}\",");
            sb.AppendLine($"      \"HttpMethod\": \"{EscapeJson(handler.HttpMethod)}\",");
            sb.AppendLine($"      \"Route\": \"{EscapeJson(handler.Route)}\",");
            sb.AppendLine($"      \"Namespace\": \"{EscapeJson(handler.Namespace)}\",");
            sb.AppendLine($"      \"Tags\": [{string.Join(", ", handler.Tags.Select(t => $"\"{EscapeJson(t)}\""))}],");
            sb.AppendLine($"      \"ClientMethodName\": \"{EscapeJson(handler.ClientMethodName)}\",");
            sb.AppendLine($"      \"HandlerType\": \"{EscapeJson(handler.HandlerType)}\"");
            sb.Append("    }");
            if (i < metadata.Handlers.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }

        sb.AppendLine("  ]");
        sb.Append("}");

        return sb.ToString();
    }

    /// <summary>
    /// Escapes special characters for JSON strings.
    /// </summary>
    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// Formats a nullable string as JSON value (null or quoted string).
    /// </summary>
    private static string JsonValue(string? value)
    {
        return value == null ? "null" : $"\"{EscapeJson(value)}\"";
    }

    /// <summary>
    /// Generates a C# source file containing the JSON metadata as a constant string.
    /// The constant string will be extracted by MSBuild to create .rpc-metadata.json
    /// Uses C# 11 raw string literals for cleaner representation.
    /// </summary>
    private static string GenerateMetadataSourceFile(string json)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// This file contains RPC metadata for client generation.");
        sb.AppendLine();
        sb.AppendLine("namespace RpcMetadataGeneration");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Contains RPC metadata JSON for this assembly.");
        sb.AppendLine("    /// This constant will be extracted by MSBuild to create .rpc-metadata.json");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static class __RpcMetadataMarker");
        sb.AppendLine("    {");
        sb.AppendLine("        // RPC_METADATA_BEGIN");
        sb.AppendLine("        public const string MetadataJson = \"\"\"");

        // Add JSON with proper indentation for C# 11 raw string literals
        // Each line must have at least the same indentation as the closing """
        foreach (var line in json.Split('\n'))
        {
            sb.AppendLine("        " + line.TrimEnd('\r'));
        }

        sb.AppendLine("        \"\"\";");
        sb.AppendLine("        // RPC_METADATA_END");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
