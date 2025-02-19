//  -------------------------------------------------------------
//  Copyright (c) 2023 Innovian Corporation. All rights reserved.
//  -------------------------------------------------------------

using System.Text.Json.Serialization;
using BuildingBlocksPlatform.StateStore.QueryBuilder.Models.Enums;

namespace BuildingBlocksPlatform.StateStore.QueryBuilder.Models;

/// <summary>
/// Configures sorting values.
/// </summary>
/// <param name="Key">The key to sort by from the state store.</param>
/// <param name="Order">An optional value indicating sorting order.</param>
public record Sorting([property: JsonPropertyName("key")] string Key, [property: JsonPropertyName("order")] Ordering Order = Ordering.Ascending);