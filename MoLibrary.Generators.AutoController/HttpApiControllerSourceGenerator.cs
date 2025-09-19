using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MoLibrary.Generators.AutoController.Constants;
using MoLibrary.Generators.AutoController.Extractors;
using MoLibrary.Generators.AutoController.Generators;
using MoLibrary.Generators.AutoController.Helpers;

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

        // Extract configuration from assembly-level attributes
        var configProvider = context.CompilationProvider
            .Select((compilation, token) => ConfigurationHelper.ExtractConfiguration(compilation));

        // Transform each candidate class into a HandlerCandidate
        var handlerCandidates = classDeclarations
            .Combine(context.CompilationProvider)
            .Combine(configProvider)
            .Select((tuple, token) =>
            {
                var (classDeclaration, compilation) = tuple.Left;
                var config = tuple.Right;
                return HandlerCandidateExtractor.ExtractHandlerCandidate(classDeclaration, compilation, config);
            })
            .Where(candidate => candidate is not null)
            .Collect();

        // After processing all candidates, generate controllers grouped by Route and HandlerType
        context.RegisterSourceOutput(handlerCandidates, (spc, candidates) =>
        {
            ControllerCodeGenerator.GenerateControllers(spc, candidates.Where(c => c is not null).Select(c => c!).ToList());
        });
    }
}
