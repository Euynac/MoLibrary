using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.TypeDiscovery.Models;

namespace Monica.Core.TypeDiscovery.Services;

/// <summary>
/// Compiles module type queries into immutable match sets without executing module commit callbacks.
/// </summary>
internal static class TypeDiscoveryCompiler
{
    /// <summary>
    /// Freezes <paramref name="plans"/> and evaluates their structurally distinct queries in one pass over the cached
    /// business-type list.
    /// </summary>
    /// <param name="types">
    /// The stable, host-owned type snapshot. Its order determines result order and therefore must not change during
    /// compilation.
    /// </param>
    /// <param name="plans">Module plans in stable topology and registration order.</param>
    /// <returns>An analysis-only compilation whose matches can later be committed plan by plan.</returns>
    /// <remarks>
    /// This method performs reflection analysis only. It does not invoke commit callbacks or mutate
    /// <c>IServiceCollection</c>. Duplicate type entries and equivalent queries are evaluated once.
    /// </remarks>
    internal static TypeDiscoveryCompilation Compile(
        IReadOnlyList<Type> types,
        IReadOnlyList<ITypeDiscoveryPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(plans);

        var queries = CollectQueries(plans);
        if (queries.Count == 0)
        {
            return new TypeDiscoveryCompilation(
                new Dictionary<TypeQuery, IReadOnlyList<BusinessTypeMatch>>(),
                [],
                enumeratedTypeCount: 0,
                excludedTypeCount: 0);
        }

        var requirements = TypeFactRequirements.CustomAttributes;
        foreach (var query in queries)
        {
            requirements |= query.Requirements;
        }

        var matchesByQuery = new List<BusinessTypeMatch>[queries.Count];
        var genericMatchBuffers = (requirements & TypeFactRequirements.OpenGenericInterfaces) != 0
            ? new List<OpenGenericInterfaceMatch>?[queries.Count]
            : [];
        for (var queryIndex = 0; queryIndex < queries.Count; queryIndex++)
        {
            matchesByQuery[queryIndex] = [];
            if (genericMatchBuffers.Length != 0 &&
                (queries[queryIndex].Requirements & TypeFactRequirements.OpenGenericInterfaces) != 0)
            {
                genericMatchBuffers[queryIndex] = [];
            }
        }

        var scannedTypes = new HashSet<Type>();
        var excludedTypeCount = 0;
        foreach (var type in types)
        {
            ArgumentNullException.ThrowIfNull(type);
            if (!scannedTypes.Add(type))
            {
                continue;
            }

            var shape = new BusinessTypeShape(type);
            try
            {
                if (shape.HasAttribute(typeof(ExcludeFromBusinessTypeDiscoveryAttribute), inherit: false))
                {
                    excludedTypeCount++;
                    continue;
                }
            }
            catch (Exception exception) when (exception is not TypeDiscoveryScanException)
            {
                throw TypeDiscoveryScanException.ForTypePreparation(type, exception);
            }

            // Queries retain their structural order, so inexpensive classification nodes can reject a type before
            // interface, hierarchy, or attribute facts are requested. Each requested fact is then cached by the shape.
            for (var queryIndex = 0; queryIndex < queries.Count; queryIndex++)
            {
                var query = queries[queryIndex];
                var genericMatches = genericMatchBuffers.Length == 0 ? null : genericMatchBuffers[queryIndex];
                genericMatches?.Clear();

                bool matched;
                try
                {
                    matched = query.Evaluate(shape, genericMatches);
                }
                catch (Exception exception) when (exception is not TypeDiscoveryScanException)
                {
                    throw TypeDiscoveryScanException.ForQuery(type, query, exception);
                }

                if (!matched)
                {
                    continue;
                }

                IReadOnlyList<OpenGenericInterfaceMatch> capturedGenericMatches =
                    genericMatches is { Count: > 0 }
                        ? Array.AsReadOnly(genericMatches.ToArray())
                        : Array.Empty<OpenGenericInterfaceMatch>();

                matchesByQuery[queryIndex].Add(new BusinessTypeMatch(shape, capturedGenericMatches));
            }
        }

        var compiledMatches = new Dictionary<TypeQuery, IReadOnlyList<BusinessTypeMatch>>(queries.Count);
        for (var queryIndex = 0; queryIndex < queries.Count; queryIndex++)
        {
            compiledMatches.Add(queries[queryIndex], matchesByQuery[queryIndex].AsReadOnly());
        }

        return new TypeDiscoveryCompilation(
            compiledMatches,
            queries,
            scannedTypes.Count,
            excludedTypeCount);
    }

    private static IReadOnlyList<TypeQuery> CollectQueries(IReadOnlyList<ITypeDiscoveryPlan> plans)
    {
        var queries = new List<TypeQuery>();
        var distinctQueries = new HashSet<TypeQuery>();

        foreach (var plan in plans)
        {
            ArgumentNullException.ThrowIfNull(plan);
            plan.Freeze();

            foreach (var registration in plan.Registrations)
            {
                if (distinctQueries.Add(registration.Query))
                {
                    queries.Add(registration.Query);
                }
            }
        }

        return queries.AsReadOnly();
    }
}

/// <summary>
/// Stores immutable match sets produced by one analysis-only discovery compilation.
/// </summary>
internal sealed class TypeDiscoveryCompilation
{
    private IReadOnlyDictionary<TypeQuery, IReadOnlyList<BusinessTypeMatch>>? _matchesByQuery;
    private IReadOnlyList<TypeQuery> _queries;

    internal TypeDiscoveryCompilation(
        IReadOnlyDictionary<TypeQuery, IReadOnlyList<BusinessTypeMatch>> matchesByQuery,
        IReadOnlyList<TypeQuery> queries,
        int enumeratedTypeCount,
        int excludedTypeCount)
    {
        _matchesByQuery = matchesByQuery;
        _queries = queries;
        EnumeratedTypeCount = enumeratedTypeCount;
        ExcludedTypeCount = excludedTypeCount;
        MatchCount = matchesByQuery.Values.Sum(static matches => matches.Count);
    }

    internal IReadOnlyList<TypeQuery> Queries => _queries;

    internal int EnumeratedTypeCount { get; }

    internal int ExcludedTypeCount { get; }

    internal int MatchCount { get; }

    internal IReadOnlyList<BusinessTypeMatch> GetMatches(TypeQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var matchesByQuery = _matchesByQuery
            ?? throw new InvalidOperationException("The type-discovery compilation has already been released.");
        return matchesByQuery.TryGetValue(query, out var matches)
            ? matches
            : throw new InvalidOperationException($"The structural query '{query}' was not part of this compilation.");
    }

    internal void Release()
    {
        _matchesByQuery = null;
        _queries = [];
    }
}

/// <summary>
/// Reports a reflection failure with the business type and structural query that caused it.
/// </summary>
internal sealed class TypeDiscoveryScanException : InvalidOperationException
{
    private TypeDiscoveryScanException(string message, Type businessType, TypeQuery? query, Exception innerException)
        : base(message, innerException)
    {
        BusinessType = businessType;
        Query = query;
    }

    internal Type BusinessType { get; }

    internal TypeQuery? Query { get; }

    internal static TypeDiscoveryScanException ForTypePreparation(Type type, Exception exception)
    {
        return new TypeDiscoveryScanException(
            $"Failed to prepare structural facts for business type '{type.FullName ?? type.Name}'.",
            type,
            query: null,
            exception);
    }

    internal static TypeDiscoveryScanException ForQuery(Type type, TypeQuery query, Exception exception)
    {
        return new TypeDiscoveryScanException(
            $"Failed to evaluate business type '{type.FullName ?? type.Name}' for structural query '{query}'.",
            type,
            query,
            exception);
    }
}
