//  -------------------------------------------------------------
//  Copyright (c) 2023 Innovian Corporation. All rights reserved.
//  -------------------------------------------------------------

namespace MoLibrary.StateStore.QueryBuilder.Interfaces;

public interface IInitialQueryBuilder<T>
{
    IPagingQuery<T> WithPaging(uint limit);
    IPagingQuery<T> WithPaging(string continuationToken);
    IPagingQuery<T> WithPaging(uint limit, string continuationToken);
    ISortByQuery<T> Where(Func<FilterQuery<T>, IFinishedFilterQuery> filterAction);
}