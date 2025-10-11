using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MoLibrary.Generators.AutoController.Constants;
using MoLibrary.Generators.AutoController.Diagnostics;
using MoLibrary.Generators.AutoController.Extractors;
using MoLibrary.Generators.AutoController.Generators;
using MoLibrary.Generators.AutoController.Helpers;
using MoLibrary.Generators.AutoController.Models;

namespace MoLibrary.Generators.AutoController;

/// <summary>
/// Source generator that creates HTTP API controllers from application service handlers.
/// 
/// References:
/// - https://www.cnblogs.com/fanshaoO/p/18101185
/// - https://stackoverflow.com/questions/76891987/must-restart-visual-studio-for-source-generator-files-to-be-picked-up
/// 
/// Note: After first generation, subsequent project rebuilds may not regenerate. 
/// Visual Studio restart may be required to pick up the latest generator changes.
/// </summary>
[Generator]
public class HttpApiControllerSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        //Debugger.Launch();
        // Filter classes that derive from an application service
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax { BaseList: { } },
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
            .Where(static c => c.BaseList!.Types.Any(t => t.ToString().Contains(GeneratorConstants.ClassNames.ApplicationService)));

        // Extract configuration from assembly-level attributes with error reporting
        var configProvider = context.CompilationProvider
            .Select((compilation, token) => ConfigurationHelper.ExtractConfiguration(compilation));

        // Transform each candidate class into a HandlerCandidate with comprehensive error reporting
        var handlerCandidatesWithErrors = classDeclarations
            .Combine(context.CompilationProvider)
            .Combine(configProvider)
            .Select((tuple, token) =>
            {
                var (classDeclaration, compilation) = tuple.Left;
                var (config, errorMessage) = tuple.Right;
                return new { classDeclaration, compilation, config, errorMessage };
            })
            .Collect();

        // Process all candidates and generate controllers with error handling
        context.RegisterSourceOutput(handlerCandidatesWithErrors, (spc, candidatesInfo) =>
        {
            var validCandidates = new List<HandlerCandidate>();

            // Report configuration errors once per compilation, not per class
            var processedConfigErrors = new HashSet<string>();
            foreach (var info in candidatesInfo)
            {
                // Check if generation should be skipped for this assembly
                if (info.config?.SkipGeneration is true)
                {
                    break; 
                }
                // Report configuration error only once per unique error message
                if (info.errorMessage != null && processedConfigErrors.Add(info.errorMessage))
                {
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.ConfigurationExtractionFailed,
                        Location.None,
                        info.errorMessage);
                    spc.ReportDiagnostic(diagnostic);
                }

                // Skip if configuration extraction failed
                if (info.config == null)
                    continue; // Configuration extraction failed, error already reported

                // Extract handler candidate with error reporting
                var candidate = HandlerCandidateExtractor.ExtractHandlerCandidate(
                    info.classDeclaration,
                    info.compilation,
                    info.config,
                    spc);

                if (candidate != null)
                {
                    validCandidates.Add(candidate);
                }
                // If candidate is null, errors have already been reported by the extractor
            }

            // Generate controllers only if we have valid candidates
            if (validCandidates.Any())
            {
                ControllerCodeGenerator.GenerateControllers(spc, validCandidates);

                // Generate RPC metadata file for client code generation
                // Extract config and compilation from first candidate's context
                var firstInfo = candidatesInfo.FirstOrDefault();
                if (firstInfo != null && firstInfo.config != null)
                {
                    MetadataFileGenerator.GenerateMetadataFile(
                        spc,
                        validCandidates,
                        firstInfo.config,
                        firstInfo.compilation);
                }
            }
        });
    }
}
