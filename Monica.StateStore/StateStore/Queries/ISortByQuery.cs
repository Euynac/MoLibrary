//  -------------------------------------------------------------
//  Copyright (c) 2023 Innovian Corporation. All rights reserved.
//  -------------------------------------------------------------

using System.Linq.Expressions;

namespace Monica.StateStore.StateStore.Queries;

public interface ISortByQuery<T>
{
    ISortByQuery<T> Sort(Expression<Func<T, string>> propertyName, Ordering? direction = null);
    IFinishedQueryBuilder<T> Build();
}