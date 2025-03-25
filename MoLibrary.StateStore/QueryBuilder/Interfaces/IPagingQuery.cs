//  -------------------------------------------------------------
//  Copyright (c) 2023 Innovian Corporation. All rights reserved.
//  -------------------------------------------------------------

using System.Linq.Expressions;
using MoLibrary.StateStore.QueryBuilder;
using MoLibrary.StateStore.QueryBuilder.Models.Enums;

namespace MoLibrary.StateStore.QueryBuilder.Interfaces;

public interface IPagingQuery<T>
{
    ISortByQuery<T> Where(Func<FilterQuery<T>, IFinishedFilterQuery> filterAction);
    ISortByQuery<T> Sort(Expression<Func<T, string>> propertyName, Ordering? direction = null);
}