//  -------------------------------------------------------------
//  Copyright (c) 2023 Innovian Corporation. All rights reserved.
//  -------------------------------------------------------------

using System.Linq.Expressions;

namespace Monica.StateStore.Queries;

/// <summary>
/// Configures sorting values.
/// </summary>
/// <param name="Property">The strongly typed property path to sort by.</param>
/// <param name="Order">An optional value indicating sorting order.</param>
public sealed record Sorting(LambdaExpression Property, Ordering Order = Ordering.Ascending);
