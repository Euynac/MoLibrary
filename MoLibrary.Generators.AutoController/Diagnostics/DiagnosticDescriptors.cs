using Microsoft.CodeAnalysis;

namespace MoLibrary.Generators.AutoController.Diagnostics;

internal static class DiagnosticDescriptors
{
    private const string Category = "AutoController";

    public static readonly DiagnosticDescriptor MissingConfigurationAttribute = new DiagnosticDescriptor(
        id: "AC0001",
        title: "Missing AutoControllerGeneratorConfig attribute",
        messageFormat: "AutoController generator requires [assembly: AutoControllerGeneratorConfig] attribute when RequireExplicitRoutes is not set. Add it to Program.cs or AssemblyInfo.cs with DefaultRoutePrefix configuration.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The AutoController generator needs configuration to generate default routes. Add [assembly: AutoControllerGeneratorConfig(DefaultRoutePrefix = \"api/v1\")] to your Program.cs or AssemblyInfo.cs file.");

    public static readonly DiagnosticDescriptor InvalidDefaultRoutePrefix = new DiagnosticDescriptor(
        id: "AC0002",
        title: "Invalid DefaultRoutePrefix configuration",
        messageFormat: "AutoController generator: DefaultRoutePrefix '{0}' is invalid. It must be a non-empty string without leading/trailing slashes.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The DefaultRoutePrefix must be a valid route segment without leading or trailing slashes.");

    public static readonly DiagnosticDescriptor InvalidApplicationServiceInheritance = new DiagnosticDescriptor(
        id: "AC0003",
        title: "Invalid ApplicationService inheritance",
        messageFormat: "AutoController generator: Class '{0}' does not properly inherit from ApplicationService<TRequest, TResponse>. Ensure the base class has exactly 2 generic type arguments.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Classes processed by AutoController generator must inherit from ApplicationService<TRequest, TResponse> with exactly 2 generic type arguments.");

    public static readonly DiagnosticDescriptor MissingHandleMethod = new DiagnosticDescriptor(
        id: "AC0004",
        title: "Missing Handle method",
        messageFormat: "AutoController generator: Class '{0}' does not contain a public Handle method. Add a public method that handles the request.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ApplicationService classes must contain a public Handle method to be processed by the AutoController generator.");

    public static readonly DiagnosticDescriptor InvalidHandleMethodSignature = new DiagnosticDescriptor(
        id: "AC0005",
        title: "Invalid Handle method signature",
        messageFormat: "AutoController generator: Handle method in class '{0}' has invalid signature. Expected signature: 'public Task<TResponse> Handle(TRequest request)' or 'public async Task<TResponse> Handle(TRequest request)'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Handle method must return Task<TResponse> and accept a single TRequest parameter.");

    public static readonly DiagnosticDescriptor MissingHttpMethodAttribute = new DiagnosticDescriptor(
        id: "AC0006",
        title: "Missing HTTP method attribute",
        messageFormat: "AutoController generator: Method '{0}' in class '{1}' requires an HTTP method attribute ([HttpGet], [HttpPost], etc.) or follow CQRS naming convention (Query*/Command* prefix).",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Methods must either have HTTP method attributes or follow CQRS naming conventions for automatic HTTP method detection.");

    public static readonly DiagnosticDescriptor RouteConflict = new DiagnosticDescriptor(
        id: "AC0007",
        title: "Route conflict detected",
        messageFormat: "Route conflict detected. Multiple handlers use route '{0}' with {1} method: {2}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Multiple handlers cannot use the same route with the same HTTP method. Ensure each handler has a unique route or HTTP method combination.");

    public static readonly DiagnosticDescriptor ConfigurationExtractionFailed = new DiagnosticDescriptor(
        id: "AC0008",
        title: "Configuration extraction failed",
        messageFormat: "Failed to extract configuration from assembly attributes. Error: {0}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generator failed to read the AutoControllerGeneratorConfig attribute. Check the attribute syntax and values.");

    public static readonly DiagnosticDescriptor HandlerExtractionFailed = new DiagnosticDescriptor(
        id: "AC0009",
        title: "Handler extraction failed",
        messageFormat: "Failed to extract handler information from class '{0}'. Error: {1}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generator failed to extract the necessary information from a handler class. Check the class structure and inheritance.");

    public static readonly DiagnosticDescriptor CodeGenerationFailed = new DiagnosticDescriptor(
        id: "AC0010",
        title: "Code generation failed",
        messageFormat: "Failed to generate controller code for route '{0}' and handler type '{1}'. Error: {2}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generator failed to produce the controller code. This is usually due to invalid handler structure or configuration.");

    public static readonly DiagnosticDescriptor InvalidRouteTemplate = new DiagnosticDescriptor(
        id: "AC0011",
        title: "Invalid route template",
        messageFormat: "AutoController generator: Invalid route template '{0}' in class '{1}'. Route templates must be valid ASP.NET Core route patterns.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Route templates must follow ASP.NET Core routing conventions. Avoid invalid characters or malformed route patterns.");

    public static readonly DiagnosticDescriptor UnknownHandlerType = new DiagnosticDescriptor(
        id: "AC0012",
        title: "Unknown handler type",
        messageFormat: "AutoController generator: Cannot determine handler type for class '{0}'. Class name must contain 'CommandHandler' or 'QueryHandler', or start with 'Command' or 'Query'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Handler classes must follow naming conventions to determine if they are Command or Query handlers for automatic HTTP method detection.");
}