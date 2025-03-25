//  -------------------------------------------------------------
//  Copyright (c) 2023 Innovian Corporation. All rights reserved.
//  -------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoLibrary.StateStore.QueryBuilder.Models;

/// <summary>
/// Used to serialize the built-query.
/// </summary>
public sealed record Query<TValue>(
    [property: JsonPropertyName("filter")] JsonDocument? Filter = null,
    [property: JsonPropertyName("sort")] List<Sorting>? Sorting = null,
    [property: JsonPropertyName("page")] Paging? Paging = null);