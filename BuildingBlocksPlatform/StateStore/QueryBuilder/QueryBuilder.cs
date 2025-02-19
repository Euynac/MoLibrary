//  -------------------------------------------------------------
//  Copyright (c) 2023 Innovian Corporation. All rights reserved.
//  -------------------------------------------------------------

using System.Linq.Expressions;
using System.Text.Json.Serialization;
using BuildingBlocksPlatform.StateStore.QueryBuilder.Interfaces;
using BuildingBlocksPlatform.StateStore.QueryBuilder.Models;
using BuildingBlocksPlatform.StateStore.QueryBuilder.Models.Enums;
using BuildingBlocksPlatform.StateStore.QueryBuilder.Utilities;

namespace BuildingBlocksPlatform.StateStore.QueryBuilder;

public sealed class QueryBuilder<T> : IInitialQueryBuilder<T>, IContinuableQueryBuilder<T>, ISortByQuery<T>, IFinishedQueryBuilder<T>
{
    private readonly List<Sorting> _sortQueries = new();
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private Paging _pagingQuery = new();
    private IFinishedFilterQuery? _filterQuery = null;

    /// <summary>
    /// Constructor enabling the use of the built-in JSON serializer options.
    /// </summary>
    public QueryBuilder()
    {

    }

    /// <summary>
    /// Constructor allowing the JSON serializer options to be overridden.
    /// </summary>
    /// <param name="serializerOptions"></param>
    public QueryBuilder(JsonSerializerOptions serializerOptions)
    {
        _serializerOptions = serializerOptions;
    }
    
    public IPagingQuery<T> WithPaging(uint? limit = null, string? continuationToken = null)
    {
        if (limit != null)
            _pagingQuery = _pagingQuery with {Limit = (int) limit};

        if (continuationToken != null)
            _pagingQuery = _pagingQuery with {Token = continuationToken};

        return this;
    }

    public IPagingQuery<T> WithPaging(uint limit)
    {
        _pagingQuery = new Paging((int)limit);
        return this;
    }

    public IPagingQuery<T> WithPaging(string continuationToken)
    {
        _pagingQuery = new Paging(null, continuationToken);
        return this;
    }

    public IPagingQuery<T> WithPaging(uint limit, string continuationToken)
    {
        _pagingQuery = new Paging(Limit: (int) limit, Token: continuationToken);
        return this;
    }
    
    public ISortByQuery<T> Where(Func<FilterQuery<T>, IFinishedFilterQuery> filterAction)
    {
        _filterQuery = filterAction(new FilterQuery<T>());
        return this;
    }

    public ISortByQuery<T> Sort(Expression<Func<T, string>> propertyName, Ordering? direction = null)
    {
        var jsonPropertyName = JsonNameHelpers<T>.GetJsonPropertyName(propertyName);
        _sortQueries.Add(new Sorting(jsonPropertyName, direction ?? Ordering.Ascending));
        return this;
    }
    
    public IFinishedQueryBuilder<T> Build()
    {
        return this;
    }

    public override string ToString()
    {
        var query = new Query<T>(
            Filter: _filterQuery == null ? null : JsonDocument.Parse(JsonSerializer.Serialize(_filterQuery?.GetFilter())),
            Sorting: _sortQueries.Any() ? _sortQueries : null,
            Paging: _pagingQuery != new Paging() ? _pagingQuery : null
        );

        return JsonSerializer.Serialize(query, _serializerOptions);
    }
}

public class FilterQuery<T> : IFinishedFilterQuery
{
    private readonly Dictionary<string, object> _filters = new();

    public IFinishedFilterQuery Eq(Expression<Func<T, string>> propertySelector, string value)
    {
        _filters.Add("EQ", new Dictionary<string, string> { { JsonNameHelpers<T>.GetJsonPropertyName(propertySelector), value } });
        return this;
    }

    public IFinishedFilterQuery In(Expression<Func<T, string>> propertySelector, params string[] values)
    {
        _filters.Add("IN", new Dictionary<string, string[]> { { JsonNameHelpers<T>.GetJsonPropertyName(propertySelector), values } });
        return this;
    }

    public IFinishedFilterQuery GT(Expression<Func<T, string>> propertySelector, string value)
    {
        _filters.Add("GT", new Dictionary<string, string> { { JsonNameHelpers<T>.GetJsonPropertyName(propertySelector), value } });
        return this;
    }

    public IFinishedFilterQuery GTE(Expression<Func<T, string>> propertySelector, string value)
    {
        _filters.Add("GTE", new Dictionary<string, string> { { JsonNameHelpers<T>.GetJsonPropertyName(propertySelector), value } });
        return this;
    }

    public IFinishedFilterQuery GTE(Expression<Func<T, DateTime>> propertySelector, DateTime value)
    {
        _filters.Add("GTE", new Dictionary<string, DateTime> { { JsonNameHelpers<T>.GetJsonPropertyName(propertySelector), value } });
        return this;
    }


    public IFinishedFilterQuery LT(Expression<Func<T, string>> propertySelector, string value)
    {
        _filters.Add("LT", new Dictionary<string, string> { { JsonNameHelpers<T>.GetJsonPropertyName(propertySelector), value } });
        return this;
    }

    public IFinishedFilterQuery LTE(Expression<Func<T, string>> propertySelector, string value)
    {
        _filters.Add("LTE", new Dictionary<string, string> { { JsonNameHelpers<T>.GetJsonPropertyName(propertySelector), value } });
        return this;
    }

    public FilterQuery<T> And(params IFinishedFilterQuery[] queries)
    {
        _filters.Add("AND", queries.Select(x => x.GetFilter()));
        return this;
    }

    public FilterQuery<T> Or(params IFinishedFilterQuery[] queries)
    {
        _filters.Add("OR", queries.Select(x => x.GetFilter()));
        return this;
    }

    public object GetFilter()
    {
        return _filters;
    }
}