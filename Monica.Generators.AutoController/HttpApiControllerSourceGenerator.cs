using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Monica.Generators.AutoController.Diagnostics;
using Monica.Generators.AutoController.Generators;
using Monica.Generators.AutoController.Helpers;
using Monica.Generators.AutoController.Models;

namespace Monica.Generators.AutoController;

/// <summary>
/// Generates HTTP controllers for application services from endpoint contracts declared on requests.
/// </summary>
[Generator]
public sealed class HttpApiControllerSourceGenerator : IIncrementalGenerator
{
    internal const string CANDIDATE_ANALYSIS_STEP = "ControllerCandidateAnalysis";
    internal const string CANDIDATE_COLLECTION_STEP = "ControllerCandidateCollection";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var analyzedCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                static (syntaxContext, cancellationToken) =>
                    AnalyzeCandidate(syntaxContext, cancellationToken))
            .Where(static analysis => analysis is not null)
            .Select(static (analysis, _) => analysis!)
            .WithTrackingName(CANDIDATE_ANALYSIS_STEP);

        var candidateCollection = analyzedCandidates
            .Collect()
            .WithTrackingName(CANDIDATE_COLLECTION_STEP);

        context.RegisterSourceOutput(
            candidateCollection,
            static (productionContext, analyses) => Emit(productionContext, analyses));
    }

    private static AnalysisSnapshot<ControllerCandidate>? AnalyzeCandidate(
        GeneratorSyntaxContext syntaxContext,
        System.Threading.CancellationToken cancellationToken)
    {
        var declaration = (ClassDeclarationSyntax)syntaxContext.Node;
        var fallbackLocation = declaration.Identifier.GetLocation();
        try
        {
            var handler = syntaxContext.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken)
                as INamedTypeSymbol;
            if (!ApplicationServiceSymbolHelper.TryGetContract(handler, out var request, out var handlerResult))
            {
                return null;
            }

            var diagnostics = new List<DiagnosticSnapshot>();
            foreach (var (attribute, name) in ApplicationServiceSymbolHelper.GetLegacyEndpointAttributes(handler!))
            {
                diagnostics.Add(DiagnosticSnapshot.Create(
                    DiagnosticDescriptors.LegacyHandlerAttribute,
                    attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation()
                    ?? fallbackLocation,
                    handler!.ToDisplayString(),
                    name));
            }

            if (diagnostics.Count > 0)
            {
                return new AnalysisSnapshot<ControllerCandidate>(null, diagnostics);
            }

            var endpointAttribute = EndpointModelFactory.GetEndpointAttribute(request);
            if (endpointAttribute is null)
            {
                return new AnalysisSnapshot<ControllerCandidate>(
                    null,
                    new[]
                    {
                        DiagnosticSnapshot.Create(
                            DiagnosticDescriptors.MissingEndpoint,
                            fallbackLocation,
                            request.ToDisplayString())
                    });
            }

            EndpointModelFactory.TryGetUniqueResultContract(
                request,
                out var requestResult,
                out _);
            var endpointAnalysis = EndpointModelFactory.Create(
                request,
                endpointAttribute,
                syntaxContext.SemanticModel.Compilation.Assembly,
                fallbackLocation,
                publishedOnly: false);
            if (endpointAnalysis.Value is null)
            {
                return new AnalysisSnapshot<ControllerCandidate>(null, endpointAnalysis.Diagnostics);
            }

            if (!SymbolEqualityComparer.Default.Equals(handlerResult, requestResult))
            {
                return new AnalysisSnapshot<ControllerCandidate>(
                    null,
                    new[]
                    {
                        DiagnosticSnapshot.Create(
                            DiagnosticDescriptors.HandlerResultMismatch,
                            fallbackLocation,
                            handler!.ToDisplayString(),
                            handlerResult.ToDisplayString(),
                            request.ToDisplayString(),
                            requestResult.ToDisplayString())
                    });
            }

            return new AnalysisSnapshot<ControllerCandidate>(new ControllerCandidate(
                endpointAnalysis.Value,
                ApplicationServiceSymbolHelper.GetTags(handler!).ToImmutableArray()));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new AnalysisSnapshot<ControllerCandidate>(
                null,
                new[]
                {
                    DiagnosticSnapshot.Create(
                        DiagnosticDescriptors.GenerationFailure,
                        fallbackLocation,
                        exception.ToString())
                });
        }
    }

    private static void Emit(
        SourceProductionContext context,
        ImmutableArray<AnalysisSnapshot<ControllerCandidate>> analyses)
    {
        try
        {
            foreach (var diagnostic in analyses.SelectMany(static analysis => analysis.Diagnostics))
            {
                context.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            var candidates = analyses
                .Select(static analysis => analysis.Value)
                .Where(static candidate => candidate is not null)
                .Select(static candidate => candidate!)
                .ToArray();
            if (candidates.Length > 0)
            {
                ControllerCodeGenerator.Generate(context, candidates);
            }
        }
        catch (Exception exception)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GenerationFailure,
                Location.None,
                exception.ToString()));
        }
    }
}
