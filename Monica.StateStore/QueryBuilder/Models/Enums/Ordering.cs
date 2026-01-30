//  -------------------------------------------------------------
//  Copyright (c) 2023 Innovian Corporation. All rights reserved.
//  -------------------------------------------------------------

using System.Text.Json.Serialization;
using Monica.StateStore.QueryBuilder.JsonConverters;

namespace Monica.StateStore.QueryBuilder.Models.Enums;

[JsonConverter(typeof(OrderingJsonConverter))]
public enum Ordering
{
    Ascending,
    Descending
}