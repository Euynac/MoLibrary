using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Monica.Framework.Generators.AlterItemGenerator;

/// <summary>
/// AlterItemData Source Generator
/// Automatically generate the AlterItemData class and Apply method corresponding to the entity class
/// </summary>
[Generator]
public class AlterItemDataGenerator : IIncrementalGenerator
{
    private const string GenerateAlterItemDataAttributeName = "Monica.Framework.Generators.Attributes.GenerateAlterItemDataAttribute";
    private const string IMoTracingDataEntityInterfaceName = "Monica.Framework.Features.AlterChain.IMoTracingDataEntity";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        //Debugger.Launch();
        // No need to generate properties files anymore, use interface detection

        // Find classes that implement the IMoTracingDataEntity interface and check these classes first
        var tracingDataEntities = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetOptimizedEntityInfo(ctx))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        // Generate code for all detected entities (with deduplication for partial classes)
        context.RegisterSourceOutput(tracingDataEntities.Collect(), (ctx, entities) =>
        {
            // Deduplication: Use HashSet to ensure each entity is processed only once
            var uniqueEntities = new HashSet<EntityGenerationInfo>(entities);
            
            foreach (var entity in uniqueEntities)
            {
                try
                {
                    GenerateAlterItemDataForEntity(ctx, entity);
                }
                catch (Exception ex)
                {
                    // Generate diagnostic information instead of throwing exceptions
                    var diagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "MOGEN001", 
                            "AlterItemData generation failed", 
                            $"Failed to generate AlterItemData for {entity.EntitySymbol.Name}: {ex.Message}", 
                            "Monica.Generators", 
                            DiagnosticSeverity.Warning, 
                            isEnabledByDefault: true),
                        Location.None);
                    
                    ctx.ReportDiagnostic(diagnostic);
                }
            }
        });
    }

    /// <summary>
    /// Optimized entity information acquisition method: first check the IMoTracingDataEntity interface, and then check the GenerateAlterItemData property
    /// </summary>
    private static EntityGenerationInfo? GetOptimizedEntityInfo(GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classSyntax)
            return null;

        if (context.SemanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol entitySymbol)
            return null;

        // First check whether the IMoTracingDataEntity interface is implemented
        if (!ImplementsInterface(entitySymbol, IMoTracingDataEntityInterfaceName))
            return null;

        // Then check if there is a GenerateAlterItemData property and if so, use the settings in the property
        var generateAttribute = entitySymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == GenerateAlterItemDataAttributeName);

        if (generateAttribute != null)
        {
            // If there are attributes, get the attribute parameters
            var customNamespace = GetAttributeArgumentValue<string>(generateAttribute, "Namespace");
            var customClassName = GetAttributeArgumentValue<string>(generateAttribute, "ClassName");
            var includeDebugInfo = GetAttributeArgumentValue<bool>(generateAttribute, "IncludeDebugInfo");

            return new EntityGenerationInfo(
                entitySymbol,
                classSyntax,
                customNamespace,
                customClassName,
                includeDebugInfo
            );
        }

        // If there are no attributes, use the default settings
        return new EntityGenerationInfo(
            entitySymbol,
            classSyntax,
            null, // 默认命名空间
            null, // 默认类名
            false // 默认不包含调试信息
        );
    }


    /// <summary>
    /// Generate AlterItemData code for entities
    /// </summary>
    private static void GenerateAlterItemDataForEntity(SourceProductionContext context, EntityGenerationInfo entityInfo)
    {
        var entitySymbol = entityInfo.EntitySymbol;
        
        // For source generators, we can get the compilation from the containing assembly
        var compilation = entitySymbol.ContainingAssembly.Name != null ? 
            Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(entitySymbol.ContainingAssembly.Name) :
            Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("DummyCompilation");
        
        var analyzer = new EntityAnalyzer(compilation, context.CancellationToken);
        var analysisResult = analyzer.AnalyzeEntity(entitySymbol);
        
        if (analysisResult == null)
        {
            // Report analysis failed
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    "MOGEN002", 
                    "Entity analysis failed", 
                    $"Failed to analyze entity {entitySymbol.Name}", 
                    "Monica.Generators", 
                    DiagnosticSeverity.Warning, 
                    isEnabledByDefault: true),
                Location.None);
            
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // flat attribute
        var flattener = new PropertyFlattener();
        var flattenedResult = flattener.FlattenProperties(analysisResult);

        // Generate code
        var codeBuilder = new CodeBuilder();
        var generatedCode = codeBuilder.BuildAlterItemDataClass(
            analysisResult,
            flattenedResult,
            entityInfo.CustomNamespace,
            entityInfo.CustomClassName,
            entityInfo.IncludeDebugInfo);

        // Add generated source files
        var fileName = $"{entityInfo.CustomClassName ?? $"{entitySymbol.Name}AlterItemDataGen"}.g.cs";
        context.AddSource(fileName, generatedCode);
    }

    /// <summary>
    /// Check whether the type implements the specified interface
    /// </summary>
    private static bool ImplementsInterface(INamedTypeSymbol type, string interfaceName)
    {
        return type.AllInterfaces.Any(i => i.ToDisplayString() == interfaceName);
    }


    /// <summary>
    /// Get attribute parameter value
    /// </summary>
    private static T GetAttributeArgumentValue<T>(AttributeData? attribute, string parameterName)
    {
        if (attribute == null)
            return default(T)!;

        // Find named parameters
        var namedArg = attribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == parameterName);

        if (namedArg.Value.IsNull || namedArg.Value.Value == null)
            return default(T)!;

        return (T)namedArg.Value.Value;
    }

}

/// <summary>
/// Entity generation information
/// </summary>
internal class EntityGenerationInfo(
    INamedTypeSymbol entitySymbol,
    ClassDeclarationSyntax classSyntax,
    string? customNamespace = null,
    string? customClassName = null,
    bool includeDebugInfo = false)
    : IEquatable<EntityGenerationInfo>
{
    public INamedTypeSymbol EntitySymbol { get; } = entitySymbol;
    public ClassDeclarationSyntax ClassSyntax { get; } = classSyntax;
    public string? CustomNamespace { get; } = customNamespace;
    public string? CustomClassName { get; } = customClassName;
    public bool IncludeDebugInfo { get; } = includeDebugInfo;

    public bool Equals(EntityGenerationInfo? other)
    {
        if (other == null) return false;
        return SymbolEqualityComparer.Default.Equals(EntitySymbol, other.EntitySymbol);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as EntityGenerationInfo);
    }

    public override int GetHashCode()
    {
        return SymbolEqualityComparer.Default.GetHashCode(EntitySymbol);
    }
}