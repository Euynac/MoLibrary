using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Monica.Generators.AutoController.Helpers;

internal static class ApplicationServiceSymbolHelper
{
    private const string ApplicationServiceNamespace = "Monica.WebApi.Abstractions";

    /// <summary>
    /// Determines whether the class ultimately derives from a supported application-service handler base.
    /// </summary>
    public static bool IsSupportedHandlerCandidate(
        SemanticModel semanticModel,
        ClassDeclarationSyntax classDeclaration)
    {
        return IsSupportedHandlerCandidate(semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol);
    }

    /// <summary>
    /// Determines whether the symbol ultimately derives from a supported application-service handler base.
    /// </summary>
    public static bool IsSupportedHandlerCandidate(INamedTypeSymbol? typeSymbol)
    {
        for (var current = typeSymbol; current != null; current = current.BaseType)
        {
            if (IsSupportedHandlerBase(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSupportedHandlerBase(INamedTypeSymbol typeSymbol)
    {
        var definition = typeSymbol.OriginalDefinition;
        if (definition.ContainingNamespace.ToDisplayString() != ApplicationServiceNamespace)
        {
            return false;
        }

        return definition switch
        {
            { Name: "ApplicationService", Arity: 1 or 2 } => true,
            { Name: "CustomApplicationService", Arity: 2 } => true,
            _ => false
        };
    }
}
