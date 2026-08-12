//  -------------------------------------------------------------
//  Copyright (c) 2023 Innovian Corporation. All rights reserved.
//  -------------------------------------------------------------

using System.Linq.Expressions;

namespace Monica.StateStore.Queries;

public sealed class QueryBuilder<T> : IInitialQueryBuilder<T>, IContinuableQueryBuilder<T>, ISortByQuery<T>, IFinishedQueryBuilder<T>
{
    private readonly List<Sorting> _sortQueries = [];
    private Paging? _pagingQuery;
    private IFinishedFilterQuery? _filterQuery;

    /// <summary>
    /// Configures optional paging for the query.
    /// </summary>
    public IPagingQuery<T> WithPaging(uint? limit = null, string? continuationToken = null)
    {
        if (limit is not null)
        {
            _pagingQuery = (_pagingQuery ?? new Paging()) with { Limit = (int)limit };
        }

        if (continuationToken is not null)
        {
            _pagingQuery = (_pagingQuery ?? new Paging()) with { Token = continuationToken };
        }

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
        _pagingQuery = new Paging(Limit: (int)limit, Token: continuationToken);
        return this;
    }
    
    public ISortByQuery<T> Where(Func<FilterQuery<T>, IFinishedFilterQuery> filterAction)
    {
        _filterQuery = filterAction(new FilterQuery<T>());
        return this;
    }

    public ISortByQuery<T> Sort(Expression<Func<T, string>> propertyName, Ordering? direction = null)
    {
        _sortQueries.Add(new Sorting(propertyName, direction ?? Ordering.Ascending));
        return this;
    }
    
    public IFinishedQueryBuilder<T> Build()
    {
        return this;
    }

    /// <summary>
    /// Gets the provider-neutral typed query definition for protocol rendering.
    /// </summary>
    public StateQueryDefinition BuildDefinition()
    {
        return new StateQueryDefinition(
            _filterQuery?.GetFilterNode(),
            _sortQueries.Count == 0 ? [] : [.. _sortQueries],
            _pagingQuery);
    }
}

public class FilterQuery<T> : IFinishedFilterQuery
{
    private readonly List<StateFilterNode> _filters = [];

    public IFinishedFilterQuery Eq(Expression<Func<T, string>> propertySelector, string value)
    {
        _filters.Add(new StateComparisonFilterNode(StateComparisonOperator.Equal, propertySelector, value));
        return this;
    }

    public IFinishedFilterQuery In(Expression<Func<T, string>> propertySelector, params string[] values)
    {
        _filters.Add(new StateComparisonFilterNode(StateComparisonOperator.In, propertySelector, values));
        return this;
    }

    public IFinishedFilterQuery GT(Expression<Func<T, string>> propertySelector, string value)
    {
        _filters.Add(new StateComparisonFilterNode(StateComparisonOperator.GreaterThan, propertySelector, value));
        return this;
    }

    public IFinishedFilterQuery GTE(Expression<Func<T, string>> propertySelector, string value)
    {
        _filters.Add(new StateComparisonFilterNode(StateComparisonOperator.GreaterThanOrEqual, propertySelector, value));
        return this;
    }

    public IFinishedFilterQuery GTE(Expression<Func<T, DateTime>> propertySelector, DateTime value)
    {
        _filters.Add(new StateComparisonFilterNode(StateComparisonOperator.GreaterThanOrEqual, propertySelector, value));
        return this;
    }


    public IFinishedFilterQuery LT(Expression<Func<T, string>> propertySelector, string value)
    {
        _filters.Add(new StateComparisonFilterNode(StateComparisonOperator.LessThan, propertySelector, value));
        return this;
    }

    public IFinishedFilterQuery LTE(Expression<Func<T, string>> propertySelector, string value)
    {
        _filters.Add(new StateComparisonFilterNode(StateComparisonOperator.LessThanOrEqual, propertySelector, value));
        return this;
    }

    public FilterQuery<T> And(params IFinishedFilterQuery[] queries)
    {
        AddGroup(StateGroupOperator.And, queries);
        return this;
    }

    public FilterQuery<T> Or(params IFinishedFilterQuery[] queries)
    {
        AddGroup(StateGroupOperator.Or, queries);
        return this;
    }

    private void AddGroup(StateGroupOperator groupOperator, IFinishedFilterQuery[] queries)
    {
        ArgumentNullException.ThrowIfNull(queries);
        if (queries.Length == 0)
        {
            throw new ArgumentException("A state filter group must contain at least one predicate.", nameof(queries));
        }

        _filters.Add(new StateGroupFilterNode(
            groupOperator,
            queries.Select(static query => query.GetFilterNode()).ToArray()));
    }

    StateFilterNode IFinishedFilterQuery.GetFilterNode()
    {
        return _filters.Count switch
        {
            0 => throw new InvalidOperationException("A state filter must contain at least one predicate."),
            1 => _filters[0],
            _ => new StateGroupFilterNode(StateGroupOperator.And, [.. _filters])
        };
    }
}
