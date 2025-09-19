using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MoLibrary.Generators.AutoController.Constants;
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
                var config = tuple.Right;
                return new { classDeclaration, compilation, config };
            })
            .Collect();

        // Process all candidates and generate controllers with error handling
        context.RegisterSourceOutput(handlerCandidatesWithErrors, (spc, candidatesInfo) =>
        {
            var validCandidates = new List<HandlerCandidate>();

            foreach (var info in candidatesInfo)
            {
                // Extract configuration with error reporting
                var config = ConfigurationHelper.ExtractConfiguration(info.compilation, spc);
                if (config == null)
                    continue; // Configuration extraction failed, error already reported

                // Extract handler candidate with error reporting
                var candidate = HandlerCandidateExtractor.ExtractHandlerCandidate(
                    info.classDeclaration,
                    info.compilation,
                    config,
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
            }
        });
    }
}
