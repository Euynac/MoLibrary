using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Monica.Generators.AutoController.BuildTasks;

/// <summary>
/// Recreates missing RPC metadata files by scanning producer API source code before the consumer project compiles.
/// </summary>
public sealed partial class BootstrapRpcMetadataTask : Microsoft.Build.Utilities.Task
{
    [Required]
    public string ProjectDirectory { get; set; } = string.Empty;

    [Required]
    public string ConsumeDirectory { get; set; } = string.Empty;

    public override bool Execute()
    {
        try
        {
            var projectDirectory = ResolvePath(ProjectDirectory);
            var consumeDirectory = ResolvePath(projectDirectory, ConsumeDirectory);
            Directory.CreateDirectory(consumeDirectory);

            var repositoryRoot = DiscoverRepositoryRoot(projectDirectory);
            var producers = DiscoverProducerProjects(repositoryRoot, consumeDirectory);
            if (producers.Count == 0)
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "No Monica RPC producer projects were discovered for consume directory '{0}'.",
                    consumeDirectory);
                return true;
            }

            var missingProducers = producers
                .Where(producer => !File.Exists(Path.Combine(consumeDirectory, $"{producer.AssemblyName}.rpc-metadata.json")))
                .ToList();

            if (missingProducers.Count == 0)
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "No missing Monica RPC metadata files were detected for consume directory '{0}'.",
                    consumeDirectory);
                return true;
            }

            var contractTypeIndex = BuildContractTypeIndex(projectDirectory);
            var generatedFileCount = 0;
            foreach (var producerDescriptor in missingProducers)
            {
                var producer = LoadProducerProjectInfo(producerDescriptor);
                var outputFilePath = Path.Combine(consumeDirectory, $"{producer.AssemblyName}.rpc-metadata.json");
                var document = CreateBootstrapMetadata(producer, contractTypeIndex);
                if (document.Handlers.Count == 0)
                {
                    Log.LogMessage(
                        MessageImportance.Low,
                        "Skipping RPC metadata bootstrap for '{0}' because no supported handlers were found.",
                        producer.AssemblyName);
                    continue;
                }

                File.WriteAllText(outputFilePath, JsonSerializer.Serialize(document, SerializerOptions));
                generatedFileCount++;

                Log.LogMessage(
                    MessageImportance.High,
                    "Bootstrapped RPC metadata for '{0}' to '{1}'.",
                    producer.AssemblyName,
                    outputFilePath);
            }

            if (generatedFileCount > 0)
            {
                Log.LogMessage(
                    MessageImportance.High,
                    "Bootstrapped {0} Monica RPC metadata file(s) for '{1}'.",
                    generatedFileCount,
                    consumeDirectory);
            }

            return !Log.HasLoggedErrors;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true);
            return false;
        }
    }

    private static RpcMetadataDocument CreateBootstrapMetadata(
        ProducerProjectInfo producer,
        IReadOnlyDictionary<string, IReadOnlyList<string>> contractTypeIndex)
    {
        var relatedNamespaces = new HashSet<string>(PathComparer);
        var handlers = new List<HandlerMetadataDocument>();

        foreach (var sourceFilePath in producer.SourceFiles)
        {
            foreach (var handler in ParseHandlersFromSourceFile(sourceFilePath, producer.Configuration, contractTypeIndex))
            {
                if (handler.RelatedNamespaces.Count == 0)
                {
                    continue;
                }

                handlers.Add(new HandlerMetadataDocument
                {
                    RequestType = handler.RequestType,
                    ResponseType = handler.ResponseType,
                    HttpMethod = handler.HttpMethod,
                    Route = handler.Route,
                    ClientMethodName = handler.ClientMethodName,
                    HandlerType = handler.HandlerType,
                    Summary = string.Empty
                });

                foreach (var namespaceValue in handler.RelatedNamespaces)
                {
                    relatedNamespaces.Add(namespaceValue);
                }
            }
        }

        handlers.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.ClientMethodName, right.ClientMethodName));

        return new RpcMetadataDocument
        {
            AssemblyName = producer.AssemblyName,
            DomainName = producer.Configuration.DomainName,
            RoutePrefix = producer.Configuration.DefaultRoutePrefix,
            RelatedNamespaces = relatedNamespaces.OrderBy(static ns => ns, StringComparer.Ordinal).ToList(),
            Handlers = handlers
        };
    }

    private static IReadOnlyList<BootstrapHandlerInfo> ParseHandlersFromSourceFile(
        string sourceFilePath,
        AutoControllerConfigInfo configuration,
        IReadOnlyDictionary<string, IReadOnlyList<string>> contractTypeIndex)
    {
        var lines = File.ReadAllLines(sourceFilePath);
        if (lines.Length == 0)
        {
            return [];
        }

        var relatedNamespaces = ExtractContractNamespaces(lines);
        var handlers = new List<BootstrapHandlerInfo>();

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var trimmedLine = lines[lineIndex].TrimStart();
            if (trimmedLine.StartsWith("//", StringComparison.Ordinal)
                || !lines[lineIndex].Contains("class", StringComparison.Ordinal))
            {
                continue;
            }

            var declarationStart = FindDeclarationStart(lines, lineIndex);
            var declarationEnd = FindDeclarationEnd(lines, lineIndex);
            var declarationBlock = string.Join(Environment.NewLine, lines.Skip(declarationStart).Take(declarationEnd - declarationStart + 1));
            var classMatch = HandlerClassRegex().Match(declarationBlock);
            if (!classMatch.Success)
            {
                continue;
            }

            var className = classMatch.Groups["className"].Value;
            if (!TryExtractHandleMetadata(lines, declarationEnd + 1, className, out var requestType, out var responseType, out var httpMethod, out var methodRoute, out var searchEndIndex))
            {
                continue;
            }

            var handlerType = className.StartsWith("Command", StringComparison.Ordinal)
                ? "Command"
                : "Query";
            var classRoute = ExtractExplicitRoute(declarationBlock) ?? BuildDefaultClassRoute(configuration);
            var resolvedNamespaces = ResolveRelatedNamespaces(
                relatedNamespaces,
                requestType,
                responseType,
                configuration,
                contractTypeIndex);

            handlers.Add(new BootstrapHandlerInfo(
                requestType,
                responseType,
                httpMethod,
                CombineRoutes(classRoute, methodRoute),
                ComputeClientMethodName(className),
                handlerType,
                resolvedNamespaces));

            lineIndex = searchEndIndex;
        }

        return handlers;
    }

    private static IReadOnlyList<string> ResolveRelatedNamespaces(
        IReadOnlyList<string> handlerNamespaces,
        string requestType,
        string responseType,
        AutoControllerConfigInfo configuration,
        IReadOnlyDictionary<string, IReadOnlyList<string>> contractTypeIndex)
    {
        var namespaces = new HashSet<string>(handlerNamespaces, PathComparer);

        AddResolvedNamespaces(namespaces, requestType, handlerNamespaces, configuration, contractTypeIndex);
        AddResolvedNamespaces(namespaces, responseType, handlerNamespaces, configuration, contractTypeIndex);

        return namespaces
            .Where(IsContractNamespace)
            .OrderBy(static ns => ns, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddResolvedNamespaces(
        HashSet<string> namespaces,
        string typeDisplay,
        IReadOnlyList<string> handlerNamespaces,
        AutoControllerConfigInfo configuration,
        IReadOnlyDictionary<string, IReadOnlyList<string>> contractTypeIndex)
    {
        foreach (var typeName in ExtractTypeNames(typeDisplay))
        {
            if (!contractTypeIndex.TryGetValue(typeName, out var candidateNamespaces))
            {
                continue;
            }

            var preferredNamespaces = candidateNamespaces
                .Where(ns => handlerNamespaces.Contains(ns, PathComparer)
                             || handlerNamespaces.Any(existing =>
                                 ns.StartsWith(existing + ".", StringComparison.Ordinal)))
                .ToList();

            if (!string.IsNullOrWhiteSpace(configuration.DomainName))
            {
                preferredNamespaces.AddRange(candidateNamespaces.Where(ns =>
                    ns.Contains($".Domain{configuration.DomainName}", StringComparison.Ordinal)));
            }

            var chosenNamespace = preferredNamespaces
                .Distinct(PathComparer)
                .OrderBy(static ns => ns, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? candidateNamespaces.OrderBy(static ns => ns, StringComparer.Ordinal).FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(chosenNamespace))
            {
                namespaces.Add(chosenNamespace);
            }
        }
    }

    private static IEnumerable<string> ExtractTypeNames(string typeDisplay)
    {
        var matches = TypeNameRegex().Matches(typeDisplay);
        foreach (Match match in matches)
        {
            var typeName = match.Groups["typeName"].Value;
            if (IgnoredTypeNames.Contains(typeName))
            {
                continue;
            }

            yield return typeName;
        }
    }

    private static bool TryExtractHandleMetadata(
        IReadOnlyList<string> lines,
        int startIndex,
        string className,
        out string requestType,
        out string responseType,
        out string httpMethod,
        out string methodRoute,
        out int searchEndIndex)
    {
        requestType = string.Empty;
        responseType = string.Empty;
        httpMethod = string.Empty;
        methodRoute = string.Empty;
        searchEndIndex = startIndex;

        var attributeLines = new List<string>();
        for (var lineIndex = startIndex; lineIndex < lines.Count; lineIndex++)
        {
            var trimmedLine = lines[lineIndex].Trim();
            if (trimmedLine.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmedLine.StartsWith("[", StringComparison.Ordinal))
            {
                attributeLines.Add(trimmedLine);
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                continue;
            }

            var signatureEndIndex = lineIndex;
            var signatureBuilder = new System.Text.StringBuilder();
            while (signatureEndIndex < lines.Count)
            {
                signatureBuilder.AppendLine(lines[signatureEndIndex].Trim());
                if (lines[signatureEndIndex].Contains("{", StringComparison.Ordinal)
                    || lines[signatureEndIndex].Contains("=>", StringComparison.Ordinal)
                    || lines[signatureEndIndex].Contains(';', StringComparison.Ordinal))
                {
                    break;
                }

                signatureEndIndex++;
            }

            var signature = signatureBuilder.ToString();
            if (!signature.Contains("Handle(", StringComparison.Ordinal))
            {
                attributeLines.Clear();
                lineIndex = signatureEndIndex;
                continue;
            }

            var methodMatch = HandleSignatureRegex().Match(signature);
            if (!methodMatch.Success)
            {
                attributeLines.Clear();
                lineIndex = signatureEndIndex;
                continue;
            }

            requestType = NormalizeTypeDisplay(methodMatch.Groups["requestType"].Value);
            responseType = NormalizeTypeDisplay(methodMatch.Groups["responseType"].Value);

            var explicitHttpMethod = TryExtractHttpMethod(attributeLines);
            if (explicitHttpMethod == null)
            {
                httpMethod = className.StartsWith("Query", StringComparison.Ordinal) ? "GET" : "POST";
                methodRoute = ExtractCqrsMethodRoute(className);
            }
            else
            {
                httpMethod = explicitHttpMethod.Value.HttpMethod;
                methodRoute = explicitHttpMethod.Value.Route;
            }

            searchEndIndex = signatureEndIndex;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<string> ExtractContractNamespaces(IEnumerable<string> lines)
    {
        return lines
            .Select(static line => line.Trim())
            .Where(static line =>
                line.StartsWith("using ", StringComparison.Ordinal)
                && line.EndsWith(";", StringComparison.Ordinal)
                && !line.Contains("=", StringComparison.Ordinal))
            .Select(static line => line["using ".Length..^1].Trim())
            .Where(IsContractNamespace)
            .Distinct(PathComparer)
            .OrderBy(static ns => ns, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildContractTypeIndex(string projectDirectory)
    {
        var result = new Dictionary<string, HashSet<string>>(PathComparer);
        foreach (var sourceFilePath in EnumerateContractSourceFiles(projectDirectory))
        {
            var content = File.ReadAllText(sourceFilePath);
            var namespaceMatch = NamespaceRegex().Match(content);
            if (!namespaceMatch.Success)
            {
                continue;
            }

            var namespaceValue = namespaceMatch.Groups["namespace"].Value;
            if (!IsContractNamespace(namespaceValue))
            {
                continue;
            }

            foreach (Match typeMatch in TypeDeclarationRegex().Matches(content))
            {
                var typeName = typeMatch.Groups["typeName"].Value;
                if (!result.TryGetValue(typeName, out var namespaces))
                {
                    namespaces = new HashSet<string>(PathComparer);
                    result[typeName] = namespaces;
                }

                namespaces.Add(namespaceValue);
            }
        }

        return result.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value.OrderBy(static ns => ns, StringComparer.Ordinal).ToArray(),
            PathComparer);
    }

    private static IReadOnlyList<string> EnumerateContractSourceFiles(string projectDirectory)
    {
        var contractDirectory = Path.Combine(projectDirectory, "PublishedLanguages");
        if (!Directory.Exists(contractDirectory))
        {
            return EnumerateSourceFiles(projectDirectory);
        }

        return Directory
            .EnumerateFiles(contractDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !IsUnderExcludedDirectory(path))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsContractNamespace(string namespaceValue)
    {
        return namespaceValue.Contains(".PublishedLanguages.", StringComparison.Ordinal)
               || namespaceValue.EndsWith(".PublishedLanguages", StringComparison.Ordinal);
    }

    private static string? ExtractExplicitRoute(string declarationBlock)
    {
        var routeMatch = RouteAttributeRegex().Match(declarationBlock);
        return routeMatch.Success
            ? routeMatch.Groups["route"].Value
            : null;
    }

    private static (string HttpMethod, string Route)? TryExtractHttpMethod(IEnumerable<string> attributeLines)
    {
        var attributeBlock = string.Join(Environment.NewLine, attributeLines);
        var methodMatch = HttpMethodAttributeRegex().Match(attributeBlock);
        if (!methodMatch.Success)
        {
            return null;
        }

        var httpMethod = methodMatch.Groups["method"].Value
            .Replace("Http", string.Empty, StringComparison.Ordinal)
            .Replace("Attribute", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        return (httpMethod, methodMatch.Groups["route"].Value);
    }

    private static string BuildDefaultClassRoute(AutoControllerConfigInfo configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.DefaultRoutePrefix))
        {
            return configuration.DomainName ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(configuration.DomainName))
        {
            return configuration.DefaultRoutePrefix!;
        }

        return $"{configuration.DefaultRoutePrefix.TrimEnd('/')}/{configuration.DomainName}";
    }

    private static string CombineRoutes(string classRoute, string methodRoute)
    {
        if (string.IsNullOrWhiteSpace(methodRoute))
        {
            return classRoute;
        }

        if (string.IsNullOrWhiteSpace(classRoute))
        {
            return methodRoute.Trim('/');
        }

        return $"{classRoute.TrimEnd('/')}/{methodRoute.TrimStart('/')}";
    }

    private static string ComputeClientMethodName(string className)
    {
        return className
            .Replace("CommandHandler", string.Empty, StringComparison.Ordinal)
            .Replace("QueryHandler", string.Empty, StringComparison.Ordinal);
    }

    private static string ExtractCqrsMethodRoute(string className)
    {
        var methodName = ComputeClientMethodName(className);
        if (methodName.StartsWith("Command", StringComparison.Ordinal))
        {
            methodName = methodName["Command".Length..];
        }
        else if (methodName.StartsWith("Query", StringComparison.Ordinal))
        {
            methodName = methodName["Query".Length..];
        }

        return PascalToKebabCase(methodName);
    }

    private static string PascalToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsUpper(current) && index > 0)
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }

    private static string NormalizeTypeDisplay(string value)
    {
        return WhitespaceRegex()
            .Replace(value, string.Empty)
            .Replace("global::", string.Empty, StringComparison.Ordinal);
    }

    private static int FindDeclarationStart(IReadOnlyList<string> lines, int lineIndex)
    {
        var currentIndex = lineIndex;
        while (currentIndex > 0)
        {
            var trimmedLine = lines[currentIndex - 1].Trim();
            if (trimmedLine.Length == 0
                || trimmedLine.StartsWith("[", StringComparison.Ordinal)
                || trimmedLine.StartsWith("///", StringComparison.Ordinal))
            {
                currentIndex--;
                continue;
            }

            break;
        }

        return currentIndex;
    }

    private static int FindDeclarationEnd(IReadOnlyList<string> lines, int lineIndex)
    {
        var currentIndex = lineIndex;
        while (currentIndex < lines.Count - 1 && !lines[currentIndex].Contains("{", StringComparison.Ordinal))
        {
            currentIndex++;
        }

        return currentIndex;
    }

    private static List<ProducerProjectDescriptor> DiscoverProducerProjects(string repositoryRoot, string consumeDirectory)
    {
        var projects = new List<ProducerProjectDescriptor>();

        foreach (var projectFilePath in Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories))
        {
            if (IsUnderExcludedDirectory(projectFilePath))
            {
                continue;
            }

            var producer = TryCreateProducerProjectDescriptor(projectFilePath, consumeDirectory);
            if (producer != null)
            {
                projects.Add(producer);
            }
        }

        projects.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.AssemblyName, right.AssemblyName));
        return projects;
    }

    private static ProducerProjectDescriptor? TryCreateProducerProjectDescriptor(string projectFilePath, string consumeDirectory)
    {
        var document = XDocument.Load(projectFilePath);
        var projectDirectory = Path.GetDirectoryName(projectFilePath)
                               ?? throw new InvalidOperationException($"Unable to resolve project directory for '{projectFilePath}'.");

        var exportDirectory = FindFirstPropertyValue(document.Root, "MonicaRpcMetadataExportDirectory");
        if (string.IsNullOrWhiteSpace(exportDirectory))
        {
            return null;
        }

        var resolvedExportDirectory = ResolvePath(projectDirectory, exportDirectory);
        if (!string.Equals(
                resolvedExportDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                consumeDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                PathComparison))
        {
            return null;
        }

        var assemblyName = FindFirstPropertyValue(document.Root, "AssemblyName");
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            assemblyName = Path.GetFileNameWithoutExtension(projectFilePath);
        }

        return new ProducerProjectDescriptor(
            assemblyName!,
            projectDirectory);
    }

    private static ProducerProjectInfo LoadProducerProjectInfo(ProducerProjectDescriptor producer)
    {
        var sourceFiles = EnumerateSourceFiles(producer.ProjectDirectory);
        var configuration = ParseAutoControllerConfiguration(sourceFiles, producer.AssemblyName);
        return new ProducerProjectInfo(
            producer.AssemblyName,
            producer.ProjectDirectory,
            configuration,
            sourceFiles);
    }

    private static AutoControllerConfigInfo ParseAutoControllerConfiguration(
        IReadOnlyList<string> sourceFiles,
        string assemblyName)
    {
        string? routePrefix = null;
        string? domainName = null;

        foreach (var sourceFilePath in sourceFiles)
        {
            var content = File.ReadAllText(sourceFilePath);
            var configMatch = AutoControllerConfigRegex().Match(content);
            if (!configMatch.Success)
            {
                continue;
            }

            var argumentBlock = configMatch.Groups["arguments"].Value;

            var routePrefixMatch = DefaultRoutePrefixRegex().Match(argumentBlock);
            if (routePrefixMatch.Success)
            {
                routePrefix = routePrefixMatch.Groups["routePrefix"].Value;
            }

            var domainMatch = DomainNameRegex().Match(argumentBlock);
            if (domainMatch.Success)
            {
                domainName = domainMatch.Groups["domainName"].Value;
            }

            if (!string.IsNullOrWhiteSpace(routePrefix) || !string.IsNullOrWhiteSpace(domainName))
            {
                break;
            }
        }

        return new AutoControllerConfigInfo(
            routePrefix ?? "api/v1",
            domainName ?? InferDomainName(assemblyName));
    }

    private static List<string> EnumerateSourceFiles(string projectDirectory)
    {
        return Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !IsUnderExcludedDirectory(path))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static string InferDomainName(string assemblyName)
    {
        return assemblyName
            .Replace(".WebAPI", string.Empty, StringComparison.Ordinal)
            .Replace(".API", string.Empty, StringComparison.Ordinal)
            .Replace("Service", string.Empty, StringComparison.Ordinal);
    }

    private static string? FindFirstPropertyValue(XElement? root, string propertyName)
    {
        return root?
            .Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, propertyName, StringComparison.Ordinal))
            ?.Value
            .Trim();
    }

    private static bool IsUnderExcludedDirectory(string path)
    {
        return path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", PathComparison)
               || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", PathComparison)
               || path.Contains($"{Path.AltDirectorySeparatorChar}bin{Path.AltDirectorySeparatorChar}", PathComparison)
               || path.Contains($"{Path.AltDirectorySeparatorChar}obj{Path.AltDirectorySeparatorChar}", PathComparison);
    }

    private static string DiscoverRepositoryRoot(string projectDirectory)
    {
        var currentDirectory = new DirectoryInfo(projectDirectory);
        while (currentDirectory != null)
        {
            if (Directory.Exists(Path.Combine(currentDirectory.FullName, ".git"))
                || currentDirectory.EnumerateFiles("*.sln").Any()
                || currentDirectory.EnumerateFiles("*.slnx").Any()
                || File.Exists(Path.Combine(currentDirectory.FullName, "Directory.Build.props")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return projectDirectory;
    }

    private static string ResolvePath(string path)
    {
        return Path.GetFullPath(path);
    }

    private static string ResolvePath(string basePath, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(basePath, path));
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    [GeneratedRegex(@"\bclass\s+(?<className>(?:Command|Query)Handler[A-Za-z0-9_]+)\b", RegexOptions.Singleline)]
    private static partial Regex HandlerClassRegex();

    [GeneratedRegex(@"Task\s*<\s*(?<responseType>.+?)\s*>\s*Handle\s*\(\s*(?<requestType>[A-Za-z0-9_\.<>]+)\s+\w+", RegexOptions.Singleline)]
    private static partial Regex HandleSignatureRegex();

    [GeneratedRegex(@"\[Route\s*\(\s*""(?<route>[^""]+)""\s*\)\]", RegexOptions.Singleline)]
    private static partial Regex RouteAttributeRegex();

    [GeneratedRegex(@"\b(?<method>HttpGet|HttpPost|HttpPut|HttpDelete|HttpPatch)(?:Attribute)?\s*(?:\(\s*""(?<route>[^""]*)""\s*\))?", RegexOptions.Singleline)]
    private static partial Regex HttpMethodAttributeRegex();

    [GeneratedRegex(@"AutoControllerConfig\s*\((?<arguments>.*?)\)", RegexOptions.Singleline)]
    private static partial Regex AutoControllerConfigRegex();

    [GeneratedRegex(@"DefaultRoutePrefix\s*=\s*""(?<routePrefix>[^""]+)""", RegexOptions.Singleline)]
    private static partial Regex DefaultRoutePrefixRegex();

    [GeneratedRegex(@"DomainName\s*=\s*(?:nameof\([^.)]+\.(?<domainName>[A-Za-z0-9_]+)\)|""(?<domainName>[^""]+)"")", RegexOptions.Singleline)]
    private static partial Regex DomainNameRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\bnamespace\s+(?<namespace>[A-Za-z0-9_.]+)\s*[;{]", RegexOptions.Singleline)]
    private static partial Regex NamespaceRegex();

    [GeneratedRegex(@"\b(?:record|class|interface)\s+(?:class\s+)?(?<typeName>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Singleline)]
    private static partial Regex TypeDeclarationRegex();

    [GeneratedRegex(@"\b(?<typeName>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Singleline)]
    private static partial Regex TypeNameRegex();

    private static readonly HashSet<string> IgnoredTypeNames =
    [
        "bool",
        "byte",
        "char",
        "decimal",
        "double",
        "float",
        "int",
        "long",
        "nint",
        "nuint",
        "object",
        "Res",
        "ResPaged",
        "short",
        "string",
        "Task",
        "uint",
        "ulong",
        "ushort",
        "ValueTask",
        "void"
    ];

    private sealed record ProducerProjectInfo(
        string AssemblyName,
        string ProjectDirectory,
        AutoControllerConfigInfo Configuration,
        IReadOnlyList<string> SourceFiles);

    private sealed record ProducerProjectDescriptor(
        string AssemblyName,
        string ProjectDirectory);

    private sealed record AutoControllerConfigInfo(
        string? DefaultRoutePrefix,
        string? DomainName);

    private sealed record BootstrapHandlerInfo(
        string RequestType,
        string ResponseType,
        string HttpMethod,
        string Route,
        string ClientMethodName,
        string HandlerType,
        IReadOnlyList<string> RelatedNamespaces);

    private sealed class RpcMetadataDocument
    {
        public string AssemblyName { get; set; } = string.Empty;

        public string? DomainName { get; set; }

        public string? RoutePrefix { get; set; }

        public List<string> RelatedNamespaces { get; set; } = [];

        public List<HandlerMetadataDocument> Handlers { get; set; } = [];
    }

    private sealed class HandlerMetadataDocument
    {
        public string RequestType { get; set; } = string.Empty;

        public string ResponseType { get; set; } = string.Empty;

        public string HttpMethod { get; set; } = string.Empty;

        public string Route { get; set; } = string.Empty;

        public string ClientMethodName { get; set; } = string.Empty;

        public string HandlerType { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;
    }
}
