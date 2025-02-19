//  -------------------------------------------------------------
//  Copyright (c) 2023 Innovian Corporation. All rights reserved.
//  -------------------------------------------------------------

using System.Linq.Expressions;
using BuildingBlocksPlatform.StateStore.QueryBuilder.Models.Enums;

namespace BuildingBlocksPlatform.StateStore.QueryBuilder.Interfaces;

public interface ISortByQuery<T>
{
    ISortByQuery<T> Sort(Expression<Func<T, string>> propertyName, Ordering? direction = null);
    IFinishedQueryBuilder<T> Build();
}