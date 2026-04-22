using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Monica.Generators.AutoController.Constants;
using Monica.Generators.AutoController.Helpers;
using Monica.Generators.AutoController.Models;

namespace Monica.Generators.AutoController.Generators;

/// <summary>
/// Generates RPC client interfaces and implementations from metadata.
/// </summary>
internal static class RpcClientCodeGenerator
{
    /// <summary>
    /// Generates client code for a single domain (assembly).
    /// Returns a list of (fileName, fileContent) tuples for all generated files.
    /// </summary>
    public static List<(string FileName, string Content)> GenerateClientForDomain(
        RpcMetadata metadata,
        bool addHttpImplementations,
        string? httpImplType,
        bool addLocalImplementations,
        string? localImplType)
    {
        var domainName = metadata.DomainName ?? metadata.AssemblyName.Replace(".API", "").Replace("Service", "");
        var contractNamespaceRoot = ResolveContractNamespaceRoot(metadata);
        var result = new List<(string, string)>();

        // Group handlers by type (Command/Query)
        var commandHandlers = metadata.Handlers.Where(h => h.HandlerType == "Command").ToList();
        var queryHandlers = metadata.Handlers.Where(h => h.HandlerType == "Query").ToList();

        // Generate Command interface and implementation if there are command handlers
        if (commandHandlers.Count > 0)
        {
            var commandInterfaceName = $"ICommand{domainName}";
            var interfaceCode = GenerateClientInterface(metadata, contractNamespaceRoot, commandInterfaceName, commandHandlers);
            result.Add(($"{commandInterfaceName}.g.cs", interfaceCode));

            AppendImplementations(
                result,
                metadata,
                contractNamespaceRoot,
                domainName,
                commandInterfaceName,
                commandHandlers,
                GeneratorConstants.HandlerTypes.Command,
                addHttpImplementations,
                httpImplType,
                addLocalImplementations,
                localImplType);
        }

        // Generate Query interface and implementation if there are query handlers
        if (queryHandlers.Count > 0)
        {
            var queryInterfaceName = $"IQuery{domainName}";
            var interfaceCode = GenerateClientInterface(metadata, contractNamespaceRoot, queryInterfaceName, queryHandlers);
            result.Add(($"{queryInterfaceName}.g.cs", interfaceCode));

            AppendImplementations(
                result,
                metadata,
                contractNamespaceRoot,
                domainName,
                queryInterfaceName,
                queryHandlers,
                GeneratorConstants.HandlerTypes.Query,
                addHttpImplementations,
                httpImplType,
                addLocalImplementations,
                localImplType);
        }

        return result;
    }

    private static void AppendImplementations(
        List<(string FileName, string Content)> result,
        RpcMetadata metadata,
        string contractNamespaceRoot,
        string domainName,
        string interfaceName,
        List<HandlerMetadata> handlers,
        string handlerType,
        bool addHttpImplementations,
        string? httpImplType,
        bool addLocalImplementations,
        string? localImplType)
    {
        if (addHttpImplementations)
        {
            if (string.IsNullOrWhiteSpace(httpImplType))
            {
                throw new InvalidOperationException("HTTP RPC implementation type is required when HTTP generation is enabled.");
            }

            var implementationName = NamingHelper.GenerateRpcClientImplementationName(
                domainName,
                GeneratorConstants.Transports.Http,
                handlerType);
            var implementationCode = GenerateClientImplementation(
                metadata,
                contractNamespaceRoot,
                domainName,
                interfaceName,
                implementationName,
                handlers,
                GeneratorConstants.Transports.Http,
                httpImplType);

            result.Add(($"{implementationName}.g.cs", implementationCode));
        }

        if (addLocalImplementations)
        {
            if (string.IsNullOrWhiteSpace(localImplType))
            {
                throw new InvalidOperationException("Local RPC implementation type is required when local generation is enabled.");
            }

            var implementationName = NamingHelper.GenerateRpcClientImplementationName(
                domainName,
                GeneratorConstants.Transports.Local,
                handlerType);
            var implementationCode = GenerateClientImplementation(
                metadata,
                contractNamespaceRoot,
                domainName,
                interfaceName,
                implementationName,
                handlers,
                GeneratorConstants.Transports.Local,
                localImplType);

            result.Add(($"{implementationName}.g.cs", implementationCode));
        }
    }

    /// <summary>
    /// Generates the client interface code.
    /// </summary>
    private static string GenerateClientInterface(
        RpcMetadata metadata,
        string contractNamespaceRoot,
        string interfaceName,
        List<HandlerMetadata> handlers)
    {
        var sb = new StringBuilder();

        // File header
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// This file is generated by RpcClientSourceGenerator");
        sb.AppendLine();

        AppendUsingDirectives(
            sb,
            new[]
            {
                "System.ServiceModel",
                "System.Threading.Tasks",
                "JetBrains.Annotations",
                "Monica.WebApi.RpcClient.Abstractions",
                "Monica.Core.Results"
            }.Concat(metadata.RelatedNamespaces));

        // Namespace
        var clientNamespace = $"{contractNamespaceRoot}.Contracts";
        sb.AppendLine($"namespace {clientNamespace};");
        sb.AppendLine();

        // Interface declaration with ServiceContract attribute
        sb.AppendLine("[ServiceContract]");

        // Add IRpcApi extend
        sb.AppendLine($"public interface {interfaceName} : IRpcApi");

        sb.AppendLine("{");

        // Generate interface methods
        for (int i = 0; i < handlers.Count; i++)
        {
            var handler = handlers[i];

            // Add summary comment if available
            if (!string.IsNullOrWhiteSpace(handler.Summary))
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// {handler.Summary}");
                sb.AppendLine("    /// </summary>");
            }

            // Add attributes
            sb.AppendLine("    [OperationContract]");
            sb.AppendLine("    [MustUseReturnValue]");

            // Method signature
            sb.AppendLine($"    Task<{handler.ResponseType}> {handler.ClientMethodName}({handler.RequestType} request);");

            // Add blank line between methods except for the last one
            if (i < handlers.Count - 1)
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates the client implementation code.
    /// </summary>
    private static string GenerateClientImplementation(
        RpcMetadata metadata,
        string contractNamespaceRoot,
        string domainName,
        string interfaceName,
        string implementationName,
        List<HandlerMetadata> handlers,
        string transport,
        string implementationBaseType)
    {
        var sb = new StringBuilder();

        // Extract base type name and namespace from implementationBaseType (e.g., "Monica.WebApi.RpcClient.Abstractions.HttpRpcApi")
        var baseTypeName = implementationBaseType.Contains(".")
            ? implementationBaseType.Substring(implementationBaseType.LastIndexOf('.') + 1)
            : implementationBaseType;
        var baseTypeNamespace = implementationBaseType.Contains(".")
            ? implementationBaseType.Substring(0, implementationBaseType.LastIndexOf('.'))
            : string.Empty;

        // File header
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// This file is generated by RpcClientSourceGenerator");
        sb.AppendLine();

        var interfaceNamespace = $"{contractNamespaceRoot}.Contracts";
        var usingDirectives = new List<string>
        {
            "System.Threading.Tasks",
            "Monica.DependencyInjection.Abstractions",
            "Monica.Core.Results",
            "Monica.WebApi.RpcClient.Annotations",
            interfaceNamespace
        };

        if (string.Equals(transport, GeneratorConstants.Transports.Http, StringComparison.Ordinal))
        {
            usingDirectives.Add("System.Net.Http");
            usingDirectives.Add("System.Net.Http.Json");
            usingDirectives.Add("Monica.Framework.Extensions");
        }

        if (!string.IsNullOrEmpty(baseTypeNamespace))
        {
            usingDirectives.Add(baseTypeNamespace);
        }

        usingDirectives.AddRange(metadata.RelatedNamespaces);
        AppendUsingDirectives(sb, usingDirectives);

        // Namespace
        var clientNamespace = $"{contractNamespaceRoot}.Implementations.{transport}";
        sb.AppendLine($"namespace {clientNamespace};");
        sb.AppendLine();

        // Implementation declaration
        sb.AppendLine($"[RpcClientDomain(\"{EscapeCSharpStringLiteral(domainName)}\")]");
        sb.AppendLine(GetImplementationDeclaration(implementationName, baseTypeName, interfaceName, transport));
        sb.AppendLine("{");

        // Generate implementation methods
        for (int i = 0; i < handlers.Count; i++)
        {
            var handler = handlers[i];
            GenerateClientMethod(sb, handler, transport);

            // Add blank line between methods except for the last one
            if (i < handlers.Count - 1)
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string ResolveContractNamespaceRoot(RpcMetadata metadata)
    {
        var contractNamespaceRoot = ContractNamespaceHelper.ResolveContractNamespaceRoot(
            metadata.RelatedNamespaces,
            metadata.DomainName);

        if (string.IsNullOrWhiteSpace(contractNamespaceRoot))
        {
            throw new InvalidOperationException(
                $"Unable to resolve contract namespace root for assembly '{metadata.AssemblyName}'.");
        }

        return contractNamespaceRoot!;
    }

    private static void AppendUsingDirectives(StringBuilder sb, IEnumerable<string> namespaces)
    {
        foreach (var namespaceValue in namespaces
                     .Where(static ns => !string.IsNullOrWhiteSpace(ns))
                     .Distinct(StringComparer.Ordinal))
        {
            sb.AppendLine($"using {namespaceValue};");
        }

        sb.AppendLine();
    }

    private static string EscapeCSharpStringLiteral(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string GetImplementationDeclaration(
        string implementationName,
        string baseTypeName,
        string interfaceName,
        string transport)
    {
        return transport switch
        {
            GeneratorConstants.Transports.Http
                => $"public class {implementationName}(HttpClient httpClient, ICachedServiceProvider serviceProvider) : {baseTypeName}(serviceProvider, httpClient), {interfaceName}",
            GeneratorConstants.Transports.Local
                => $"public class {implementationName}(ICachedServiceProvider serviceProvider) : {baseTypeName}(serviceProvider), {interfaceName}",
            _ => throw new InvalidOperationException($"Unsupported RPC client transport '{transport}'.")
        };
    }

    /// <summary>
    /// Generates a single client method implementation.
    /// </summary>
    private static void GenerateClientMethod(StringBuilder sb, HandlerMetadata handler, string transport)
    {
        var paramName = "request";
        // var paramName = GetParameterName(handler.RequestType);

        // No need for summary comments in implementation since the interface already has them
        // Make all methods virtual so users can override them
        sb.AppendLine($"    public virtual async Task<{handler.ResponseType}> {handler.ClientMethodName}({handler.RequestType} {paramName})");
        sb.AppendLine("    {");

        if (string.Equals(transport, GeneratorConstants.Transports.Local, StringComparison.Ordinal))
        {
            sb.AppendLine($"        return await Mediator.Send({paramName});");
            sb.AppendLine("    }");
            return;
        }

        if (!string.Equals(transport, GeneratorConstants.Transports.Http, StringComparison.Ordinal))
        {
            sb.AppendLine($"        throw new System.NotSupportedException(\"RPC transport '{EscapeCSharpStringLiteral(transport)}' is not supported by the generator.\");");
            sb.AppendLine("    }");
            return;
        }

        // Generate HTTP call based on method type
        // Special handling for object return type - throw exception for user to implement
        if (handler.ResponseType == "object")
        {
            sb.AppendLine("        throw new System.NotImplementedException(\"This method returns a file or special object. Please override this method in a derived class to provide a custom implementation.\");");
            sb.AppendLine("    }");
            return;
        }

        var httpMethod = handler.HttpMethod.ToUpperInvariant();
        var route = handler.Route;

        switch (httpMethod)
        {
            case "GET":
                // Check if the route contains route parameters (e.g., {id})
                if (route.Contains("{"))
                {
                    // Route has parameters - replace {id} with {query.Id} and append query string
                    var processedRoute = ProcessRouteWithParameters(route, paramName);
                    sb.AppendLine("        return await HttpClient.GetAsync($\"" + processedRoute + "{" + paramName + ".ToQueryString()}\", HttpCompletionOption.ResponseHeadersRead)");
                }
                else
                {
                    // No route parameters
                    sb.AppendLine("        return await HttpClient.GetAsync($\"" + route + "{" + paramName + ".ToQueryString()}\", HttpCompletionOption.ResponseHeadersRead)");
                }
                sb.AppendLine($"            .GetResponse<{handler.ResponseType}>();");
                break;

            case "POST":
                sb.AppendLine($"        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, \"{route}\") {{ Content = JsonContent.Create({paramName}) }};");
                sb.AppendLine("        return await HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead)");
                sb.AppendLine($"            .GetResponse<{handler.ResponseType}>();");
                break;

            case "PUT":
                sb.AppendLine($"        using var httpRequest = new HttpRequestMessage(HttpMethod.Put, \"{route}\") {{ Content = JsonContent.Create({paramName}) }};");
                sb.AppendLine("        return await HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead)");
                sb.AppendLine($"            .GetResponse<{handler.ResponseType}>();");
                break;

            case "DELETE":
                // For DELETE, check if it needs a body
                if (route.Contains("{"))
                {
                    var processedRoute = ProcessRouteWithParameters(route, paramName);
                    sb.AppendLine("        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $\"" + processedRoute + "{" + paramName + ".ToQueryString()}\");");
                }
                else
                {
                    sb.AppendLine("        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $\"" + route + "{" + paramName + ".ToQueryString()}\");");
                }
                sb.AppendLine("        return await HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead)");
                sb.AppendLine($"            .GetResponse<{handler.ResponseType}>();");
                break;

            default:
                sb.AppendLine($"        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, \"{route}\") {{ Content = JsonContent.Create({paramName}) }};");
                sb.AppendLine("        return await HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead)");
                sb.AppendLine($"            .GetResponse<{handler.ResponseType}>();");
                break;
        }

        sb.AppendLine("    }");
    }

    /// <summary>
    /// Gets the parameter name from a request type (converts to camelCase).
    /// </summary>
    private static string GetParameterName(string requestType)
    {
        // Extract simple type name without namespace
        var simpleTypeName = requestType.Contains(".")
            ? requestType.Substring(requestType.LastIndexOf('.') + 1)
            : requestType;

        // Remove generic arguments if any
        if (simpleTypeName.Contains("<"))
        {
            simpleTypeName = simpleTypeName.Substring(0, simpleTypeName.IndexOf('<'));
        }

        // Convert to camelCase
        if (string.IsNullOrEmpty(simpleTypeName))
            return "request";

        // Handle common prefixes
        if (simpleTypeName.StartsWith("Command"))
        {
            var withoutPrefix = simpleTypeName.Substring("Command".Length);
            return char.ToLowerInvariant(withoutPrefix[0]) + withoutPrefix.Substring(1);
        }
        else if (simpleTypeName.StartsWith("Query"))
        {
            var withoutPrefix = simpleTypeName.Substring("Query".Length);
            return char.ToLowerInvariant(withoutPrefix[0]) + withoutPrefix.Substring(1);
        }

        return char.ToLowerInvariant(simpleTypeName[0]) + simpleTypeName.Substring(1);
    }

    /// <summary>
    /// Converts a string to PascalCase.
    /// </summary>
    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpperInvariant(input[0]) + input.Substring(1);
    }

    /// <summary>
    /// Processes a route with path parameters, replacing {paramName} with {query.ParamName}.
    /// </summary>
    private static string ProcessRouteWithParameters(string route, string paramName)
    {
        // Use regex to find all route parameters like {id}, {name}, etc.
        var pattern = @"\{([^}]+)\}";
        var matches = System.Text.RegularExpressions.Regex.Matches(route, pattern);

        var result = route;
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var routeParamName = match.Groups[1].Value;
            var pascalCaseParam = ToPascalCase(routeParamName);
            // Replace {paramName} with {query.ParamName}
            result = result.Replace("{" + routeParamName + "}", "{" + paramName + "." + pascalCaseParam + "}");
        }

        return result;
    }
}
