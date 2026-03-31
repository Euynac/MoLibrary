//  -------------------------------------------------------------
//  Copyright (c) 2023 Innovian Corporation. All rights reserved.
//  -------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Monica.StateStore.StateStore.Queries;

[JsonConverter(typeof(OrderingJsonConverter))]
public enum Ordering
{
    Ascending,
    Descending
}