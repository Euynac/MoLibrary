using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MoLibrary.Framework.Generators.Attributes;

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
        Debugger.Launch();
        // 添加生成的属性文件到编译
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("GenerateAlterItemDataAttribute.g.cs", GenerateAlterItemDataAttributeSource);
        });

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
        else
        {
            // 如果没有属性，使用默认设置
            return new EntityGenerationInfo(
                entitySymbol,
                classSyntax,
                null, // 默认命名空间
                null, // 默认类名
                false // 默认不包含调试信息
            );
        }
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
        var fileName = $"{entityInfo.CustomClassName ?? $"{entitySymbol.Name}AlterItemData"}.g.cs";
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

    /// <summary>
    /// 生成 GenerateAlterItemDataAttribute 属性源码
    /// </summary>
    private static readonly string GenerateAlterItemDataAttributeSource = @"// <auto-generated/>

using System;

namespace MoLibrary.Framework.Generators.Attributes
{
    /// <summary>
    /// 标记实体类需要生成对应的 AlterItemData 类和 Apply 方法
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class GenerateAlterItemDataAttribute : Attribute
    {
        /// <summary>
        /// 生成的 AlterItemData 类的命名空间，如果不指定则使用实体类的命名空间
        /// </summary>
        public string? Namespace { get; set; }
        
        /// <summary>
        /// 生成的 AlterItemData 类名，如果不指定则使用 {EntityName}AlterItemData
        /// </summary>
        public string? ClassName { get; set; }
        
        /// <summary>
        /// 是否包含调试信息注释
        /// </summary>
        public bool IncludeDebugInfo { get; set; } = false;
    }
}";
}

/// <summary>
/// 实体生成信息
/// </summary>
internal class EntityGenerationInfo : IEquatable<EntityGenerationInfo>
{
    public EntityGenerationInfo(INamedTypeSymbol entitySymbol, ClassDeclarationSyntax classSyntax, string? customNamespace = null, string? customClassName = null, bool includeDebugInfo = false)
    {
        EntitySymbol = entitySymbol;
        ClassSyntax = classSyntax;
        CustomNamespace = customNamespace;
        CustomClassName = customClassName;
        IncludeDebugInfo = includeDebugInfo;
    }
    
    public INamedTypeSymbol EntitySymbol { get; }
    public ClassDeclarationSyntax ClassSyntax { get; }
    public string? CustomNamespace { get; }
    public string? CustomClassName { get; }
    public bool IncludeDebugInfo { get; }

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