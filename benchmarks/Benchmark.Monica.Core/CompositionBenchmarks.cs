using System.Linq.Expressions;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Extensions;
using Monica.Core.TypeDiscovery.Models;

namespace Benchmark.Monica.Core;

/// <summary>
/// Measures full host composition at representative module counts and empty discovery-plan ratios.
/// Registration delegates are generated and cached outside the measured operation.
/// </summary>
[MemoryDiagnoser]
public class CompositionBenchmarks
{
    private Action<IMonicaBuilder>[] _registrations = [];

    /// <summary>Gets or sets the number of module declarations in the composition.</summary>
    [Params(10, 50, 200)]
    public int ModuleCount { get; set; }

    /// <summary>Gets or sets the percentage of modules whose discovery declaration is empty.</summary>
    [Params(0, 50, 90)]
    public int EmptyPlanPercentage { get; set; }

    /// <summary>Builds strongly typed registration delegates outside the measured operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _registrations = CompositionBenchmarkCatalog.CreateRegistrations(
            ModuleCount,
            EmptyPlanPercentage);
    }

    /// <summary>Executes the complete Monica composition pipeline and releases its host-owned resources.</summary>
    [Benchmark]
    public int Compose()
    {
        var host = Host.CreateApplicationBuilder();
        host.AddMonica(builder =>
        {
            builder.ConfigureTypeDiscovery(options => options
                .ExcludeDefault()
                .Add(typeof(CompositionBenchmarks).Assembly));
            foreach (var register in _registrations)
            {
                register(builder);
            }
        });

        var descriptor = host.Services.Last(static service =>
            service.ServiceType == typeof(global::Monica.Core.MonicaApplication));
        var application = (global::Monica.Core.MonicaApplication)descriptor.ImplementationFactory!(
            EmptyServiceProvider.Instance);
        try
        {
            return application.Modules.RuntimeSnapshots.Count;
        }
        finally
        {
            application.Dispose();
        }
    }
}

internal sealed class EmptyServiceProvider : IServiceProvider
{
    internal static EmptyServiceProvider Instance { get; } = new();

    private EmptyServiceProvider()
    {
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType) => null;
}

internal interface IDiscoveryContributingTag;

internal sealed class DiscoveryContributingTag<T> : IDiscoveryContributingTag;

internal sealed class EmptyDiscoveryTag<T>;

internal sealed class CompositionBenchmarkModule<TTag>
    : MonicaModule<CompositionBenchmarkOptions<TTag>>
{
    public override void ValidateOptions(CompositionBenchmarkOptions<TTag> options, string? profileName)
    {
        // Composition creates a fresh bootstrap logger per operation. Keep console I/O out of the measured workload while
        // preserving the real module lifecycle and host-owned logger replacement path.
        UseCompositionLoggerFactory(NullLoggerFactory.Instance);
    }

    public override void DeclareTypeDiscovery(TypeDiscoveryPlan<CompositionBenchmarkOptions<TTag>> discovery)
    {
        if (typeof(IDiscoveryContributingTag).IsAssignableFrom(typeof(TTag)))
        {
            discovery.Match(TypeQuery.ClosedClass, static (_, _) => { });
        }
    }
}

internal sealed class CompositionBenchmarkOptions<TTag>
    : ModuleOptions<CompositionBenchmarkModule<TTag>>;

internal sealed class CompositionIndex<TFirst, TSecond, TThird>;

internal static class CompositionBenchmarkCatalog
{
    private static readonly MethodInfo ADD_MODULE = typeof(IMonicaBuilder)
        .GetMethod(nameof(IMonicaBuilder.AddModule))!;
    private static readonly Type[] DIGITS =
    [
        typeof(byte),
        typeof(short),
        typeof(int),
        typeof(long),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(Guid)
    ];
    private static readonly Type[] INDEX_TYPES = CreateIndexTypes();

    internal static Action<IMonicaBuilder>[] CreateRegistrations(
        int moduleCount,
        int emptyPlanPercentage)
    {
        return Enumerable.Range(0, moduleCount)
            .Select(index => CreateRegistration(
                INDEX_TYPES[index],
                index * 100 < moduleCount * emptyPlanPercentage))
            .ToArray();
    }

    private static Action<IMonicaBuilder> CreateRegistration(Type indexType, bool hasEmptyPlan)
    {
        var tagType = (hasEmptyPlan ? typeof(EmptyDiscoveryTag<>) : typeof(DiscoveryContributingTag<>))
            .MakeGenericType(indexType);
        var moduleType = typeof(CompositionBenchmarkModule<>).MakeGenericType(tagType);
        var optionType = typeof(CompositionBenchmarkOptions<>).MakeGenericType(tagType);
        var closedAddModule = ADD_MODULE.MakeGenericMethod(moduleType, optionType);
        var builder = Expression.Parameter(typeof(IMonicaBuilder), "builder");
        var configureType = typeof(Action<>).MakeGenericType(optionType);
        var call = Expression.Call(builder, closedAddModule, Expression.Constant(null, configureType));
        return Expression.Lambda<Action<IMonicaBuilder>>(
                Expression.Block(call, Expression.Empty()),
                builder)
            .Compile();
    }

    private static Type[] CreateIndexTypes()
    {
        var types = new List<Type>(DIGITS.Length * DIGITS.Length * DIGITS.Length);
        foreach (var first in DIGITS)
        {
            foreach (var second in DIGITS)
            {
                foreach (var third in DIGITS)
                {
                    types.Add(typeof(CompositionIndex<,,>).MakeGenericType(first, second, third));
                }
            }
        }

        return types.ToArray();
    }
}
