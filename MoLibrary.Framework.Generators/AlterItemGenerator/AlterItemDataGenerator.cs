using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MoLibrary.Framework.Generators.AlterItemGenerator;

/// <summary>
/// AlterItemData Source Generator
/// 自动生成实体类对应的 AlterItemData 类和 Apply 方法
/// </summary>
[Generator]
public class AlterItemDataGenerator : IIncrementalGenerator
{
    private const string GenerateAlterItemDataAttributeName = "MoLibrary.Framework.Generators.Attributes.GenerateAlterItemDataAttribute";
    private const string IMoTracingDataEntityInterfaceName = "MoLibrary.Framework.Features.AlterChain.IMoTracingDataEntity";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        //Debugger.Launch();
        // 不再需要生成属性文件，使用接口检测

        // 查找实现了 IMoTracingDataEntity 接口的类，优先检查这些类
        var tracingDataEntities = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetOptimizedEntityInfo(ctx))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        // Generate code for all detected entities (with deduplication for partial classes)
        context.RegisterSourceOutput(tracingDataEntities.Collect(), (ctx, entities) =>
        {
            // 去重：使用HashSet确保每个实体只处理一次
            var uniqueEntities = new HashSet<EntityGenerationInfo>(entities);
            
            foreach (var entity in uniqueEntities)
            {
                try
                {
                    GenerateAlterItemDataForEntity(ctx, entity);
                }
                catch (Exception ex)
                {
                    // 生成诊断信息而不是抛出异常
                    var diagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "MOGEN001", 
                            "AlterItemData generation failed", 
                            $"Failed to generate AlterItemData for {entity.EntitySymbol.Name}: {ex.Message}", 
                            "MoLibrary.Generators", 
                            DiagnosticSeverity.Warning, 
                            isEnabledByDefault: true),
                        Location.None);
                    
                    ctx.ReportDiagnostic(diagnostic);
                }
            }
        });
    }

    /// <summary>
    /// 优化的实体信息获取方法：优先检查IMoTracingDataEntity接口，然后检查GenerateAlterItemData属性
    /// </summary>
    private static EntityGenerationInfo? GetOptimizedEntityInfo(GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classSyntax)
            return null;

        if (context.SemanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol entitySymbol)
            return null;

        // 首先检查是否实现了 IMoTracingDataEntity 接口
        if (!ImplementsInterface(entitySymbol, IMoTracingDataEntityInterfaceName))
            return null;

        // 然后检查是否有 GenerateAlterItemData 属性，如果有，使用属性中的设置
        var generateAttribute = entitySymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == GenerateAlterItemDataAttributeName);

        if (generateAttribute != null)
        {
            // 如果有属性，获取属性参数
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

        // 如果没有属性，使用默认设置
        return new EntityGenerationInfo(
            entitySymbol,
            classSyntax,
            null, // 默认命名空间
            null, // 默认类名
            false // 默认不包含调试信息
        );
    }


    /// <summary>
    /// 为实体生成 AlterItemData 代码
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
            // 报告分析失败
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    "MOGEN002", 
                    "Entity analysis failed", 
                    $"Failed to analyze entity {entitySymbol.Name}", 
                    "MoLibrary.Generators", 
                    DiagnosticSeverity.Warning, 
                    isEnabledByDefault: true),
                Location.None);
            
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // 扁平化属性
        var flattener = new PropertyFlattener();
        var flattenedResult = flattener.FlattenProperties(analysisResult);

        // 生成代码
        var codeBuilder = new CodeBuilder();
        var generatedCode = codeBuilder.BuildAlterItemDataClass(
            analysisResult,
            flattenedResult,
            entityInfo.CustomNamespace,
            entityInfo.CustomClassName,
            entityInfo.IncludeDebugInfo);

        // 添加生成的源文件
        var fileName = $"{entityInfo.CustomClassName ?? $"{entitySymbol.Name}AlterItemDataGen"}.g.cs";
        context.AddSource(fileName, generatedCode);
    }

    /// <summary>
    /// 检查类型是否实现了指定接口
    /// </summary>
    private static bool ImplementsInterface(INamedTypeSymbol type, string interfaceName)
    {
        return type.AllInterfaces.Any(i => i.ToDisplayString() == interfaceName);
    }


    /// <summary>
    /// 获取属性参数值
    /// </summary>
    private static T GetAttributeArgumentValue<T>(AttributeData? attribute, string parameterName)
    {
        if (attribute == null)
            return default(T)!;

        // 查找命名参数
        var namedArg = attribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == parameterName);

        if (namedArg.Value.IsNull || namedArg.Value.Value == null)
            return default(T)!;

        return (T)namedArg.Value.Value;
    }

}

/// <summary>
/// 实体生成信息
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