using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.TypeDiscovery.Models;
using Monica.Core.TypeDiscovery.Services;

namespace Benchmark.Monica.Core;

/// <summary>
/// Measures the one-pass discovery compiler across the supported type and query cardinalities.
/// Dynamic type creation happens once and is excluded from the measured operation.
/// </summary>
[MemoryDiagnoser]
public class TypeDiscoveryBenchmarks
{
    private IReadOnlyList<Type> _types = [];
    private IReadOnlyList<ITypeDiscoveryPlan> _plans = [];

    /// <summary>Gets or sets the number of cached business types presented to the compiler.</summary>
    [Params(100, 1_000, 10_000)]
    public int TypeCount { get; set; }

    /// <summary>Gets or sets the number of structurally distinct queries.</summary>
    [Params(1, 5, 15, 50)]
    public int QueryCount { get; set; }

    /// <summary>Prepares stable type and query inputs outside the measured operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var corpus = DiscoveryBenchmarkCorpus.Instance;
        _types = Array.AsReadOnly(corpus.Types[..TypeCount]);

        var plan = new TypeDiscoveryPlan<DiscoveryBenchmarkOptions>();
        for (var index = 0; index < QueryCount; index++)
        {
            plan.Match(
                TypeQuery.ClosedClass.AssignableTo(corpus.MarkerInterfaces[index]),
                static (_, _) => { });
        }

        _plans = [(ITypeDiscoveryPlan)plan];
    }

    /// <summary>Compiles all distinct structural queries in a single pass over the cached type snapshot.</summary>
    [Benchmark]
    public object Compile()
    {
        return TypeDiscoveryCompiler.Compile(_types, _plans);
    }
}

/// <summary>
/// Measures release plus forced collection of a precompiled collectible discovery corpus. Dynamic assembly emission and
/// discovery compilation occur in iteration setup so the measured operation isolates release and collection behavior.
/// </summary>
[MemoryDiagnoser]
public class DiscoveryRetentionBenchmarks
{
    private const int MAX_COLLECTION_ATTEMPTS = 5;
    private RetentionIteration _iteration = null!;

    /// <summary>Creates and compiles one collectible corpus outside the measured operation.</summary>
    [IterationSetup]
    public void Setup()
    {
        _iteration = RetentionIteration.Create();
    }

    /// <summary>
    /// Releases compiler-owned reflection graphs and forces collection. A retained assembly fails the benchmark instead of
    /// appearing as an easy-to-miss Boolean result.
    /// </summary>
    [Benchmark]
    public int ReleaseAndCollect()
    {
        return _iteration.ReleaseAndCollect(MAX_COLLECTION_ATTEMPTS);
    }

    /// <summary>Releases any remaining iteration state when BenchmarkDotNet aborts an invocation.</summary>
    [IterationCleanup]
    public void Cleanup()
    {
        _iteration?.Release();
        _iteration = null!;
    }
}

internal sealed class RetentionIteration
{
    private readonly WeakReference _assemblyReference;
    private readonly TypeDiscoveryCompilation _compilation;
    private readonly ITypeDiscoveryPlan _plan;
    private bool _released;

    private RetentionIteration(
        WeakReference assemblyReference,
        TypeDiscoveryCompilation compilation,
        ITypeDiscoveryPlan plan)
    {
        _assemblyReference = assemblyReference;
        _compilation = compilation;
        _plan = plan;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static RetentionIteration Create()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"Monica.Discovery.Retention.{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.RunAndCollect);
        var module = assembly.DefineDynamicModule("Retention");
        var types = new Type[1_000];
        for (var index = 0; index < types.Length; index++)
        {
            types[index] = module.DefineType(
                    $"RetentionType{index}",
                    TypeAttributes.Public | TypeAttributes.Class)
                .CreateType()!;
        }

        var plan = new TypeDiscoveryPlan<DiscoveryBenchmarkOptions>();
        plan.Match(TypeQuery.ClosedClass, static (_, _) => { });
        var compilation = TypeDiscoveryCompiler.Compile(types, [(ITypeDiscoveryPlan)plan]);
        return new RetentionIteration(
            new WeakReference(assembly, trackResurrection: false),
            compilation,
            plan);
    }

    internal int ReleaseAndCollect(int maxAttempts)
    {
        Release();
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            if (!_assemblyReference.IsAlive)
            {
                return attempt;
            }
        }

        throw new InvalidOperationException(
            $"The collectible discovery assembly remained alive after {maxAttempts} forced collection attempts. " +
            "Compiler-owned type or query references were not released.");
    }

    internal void Release()
    {
        if (_released)
        {
            return;
        }

        _released = true;
        _compilation.Release();
        _plan.Release();
    }
}

internal sealed class DiscoveryBenchmarkOptions : IModuleOptions;

internal sealed class DiscoveryBenchmarkCorpus
{
    private const int TYPE_COUNT = 10_000;
    private const int QUERY_COUNT = 50;

    private DiscoveryBenchmarkCorpus()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Monica.Discovery.BenchmarkCorpus"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Corpus");
        MarkerInterfaces = Enumerable.Range(0, QUERY_COUNT)
            .Select(index => module.DefineType(
                    $"IBenchmarkMarker{index}",
                    TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract)
                .CreateType()!)
            .ToArray();

        Types = new Type[TYPE_COUNT];
        for (var index = 0; index < Types.Length; index++)
        {
            var builder = module.DefineType(
                $"BenchmarkType{index}",
                TypeAttributes.Public | TypeAttributes.Class);
            builder.AddInterfaceImplementation(MarkerInterfaces[index % MarkerInterfaces.Length]);
            Types[index] = builder.CreateType()!;
        }
    }

    internal static DiscoveryBenchmarkCorpus Instance { get; } = new();

    internal Type[] Types { get; }

    internal Type[] MarkerInterfaces { get; }
}
