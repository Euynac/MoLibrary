//  -------------------------------------------------------------
//  Copyright (c) 2023 Innovian Corporation. All rights reserved.
//  -------------------------------------------------------------

using System.Text.Json.Serialization;
using BuildingBlocksPlatform.StateStore.QueryBuilder.Models.Enums;

namespace BuildingBlocksPlatform.StateStore.QueryBuilder.JsonConverters;

/// <summary>
/// The JSON converter used to serialize an <see cref="Ordering"/> enum.
/// </summary>
internal sealed class OrderingJsonConverter : JsonConverter<Ordering>
{
    /// <summary>Reads and converts the JSON to type <typeparamref name="T" />.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    public override Ordering Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    /// <summary>Writes a specified value as JSON.</summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, Ordering value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case Ordering.Ascending:
                writer.WriteStringValue("ASC");
                break;
            case Ordering.Descending:
                writer.WriteStringValue("DESC");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
        }
    }
}