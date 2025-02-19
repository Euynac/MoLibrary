//  -------------------------------------------------------------
//  Copyright (c) 2023 Innovian Corporation. All rights reserved.
//  -------------------------------------------------------------

using System.Text.Json.Serialization;

namespace BuildingBlocksPlatform.StateStore.QueryBuilder.Models;

/// <summary>
/// Configures paging values.
/// </summary>
/// <param name="Limit">Sets the page size.</param>
/// <param name="Token">An iteration token returned by the component, used in subsequent queries.</param>
public record Paging([property: JsonPropertyName("limit")] int? Limit = null, [property: JsonPropertyName("token")] string? Token = null);