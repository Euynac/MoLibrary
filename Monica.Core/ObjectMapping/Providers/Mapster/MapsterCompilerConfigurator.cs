using System.Linq.Expressions;
using System.Reflection;
using ExpressionDebugger;
using FastExpressionCompiler;
using Mapster;
using Monica.Core.ObjectMapping.Models;
using Monica.Core.Results;
using Monica.Modules;

namespace Monica.Core.ObjectMapping.Providers.Mapster;

internal static class MapsterCompilerConfigurator
{
    public static void Configure(TypeAdapterConfig config, ModuleObjectMappingOption option)
    {
        config.Compiler = option.CompilerStrategy switch
        {
            ObjectMappingCompilerStrategy.Default => static expression => expression.Compile(),
            ObjectMappingCompilerStrategy.FastExpressionCompiler => CompileFast,
            ObjectMappingCompilerStrategy.Debug => expression => CompileWithDebugInfo(
                expression,
                option.DebuggerRelatedAssemblies),
            _ => throw new ArgumentOutOfRangeException(
                nameof(option.CompilerStrategy),
                option.CompilerStrategy,
                "Unknown object-mapping compiler strategy.")
        };
    }

    private static Delegate CompileFast(LambdaExpression expression)
    {
        return expression.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression);
    }

    private static Delegate CompileWithDebugInfo(
        LambdaExpression expression,
        IReadOnlyCollection<Assembly> relatedAssemblies)
    {
        return expression.CompileWithDebugInfo(
            new ExpressionCompilationOptions
            {
                EmitFile = true,
                References =
                [
                    typeof(Res).Assembly,
                    typeof(Enumerable).Assembly,
                    .. relatedAssemblies
                ]
            });
    }
}
