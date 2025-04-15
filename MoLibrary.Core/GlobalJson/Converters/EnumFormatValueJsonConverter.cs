using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoLibrary.Core.GlobalJson.Attributes;

namespace MoLibrary.Core.GlobalJson.Converters;

/// <summary>
/// JSON converter that uses <see cref="EnumFormatValueAttribute"/> to serialize/deserialize enum values.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
public class EnumFormatValueJsonConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    /// <summary>
    /// Reads and converts the JSON to an enum value using custom format values.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var value = reader.GetInt32();
            return (TEnum)Enum.ToObject(typeof(TEnum), value);
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (string.IsNullOrEmpty(stringValue))
                return default;

            if (EnumFormatValueHelper.TryParseFormattedValue<TEnum>(stringValue, out var result))
                return result;

            // Try standard enum parsing as fallback
            return Enum.TryParse<TEnum>(stringValue, true, out var parsedEnum)
                ? parsedEnum
                : default;
        }

        throw new JsonException($"Unexpected token {reader.TokenType} when parsing enum {typeof(TEnum).FullName}.");
    }

    /// <summary>
    /// Writes an enum value as JSON using custom format values.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(EnumFormatValueHelper.GetFormattedValue(value));
    }
}

/// <summary>
/// A factory for creating <see cref="EnumFormatValueJsonConverter{TEnum}"/> instances for enum types.
/// Only creates converters for enum types that have at least one member with <see cref="EnumFormatValueAttribute"/>.
/// </summary>
public class EnumFormatValueJsonConverterFactory : JsonConverterFactory
{
    // Cache to store whether an enum type has any EnumFormatValueAttribute
    private static readonly ConcurrentDictionary<Type, bool> _hasAttributeCache = new();

    /// <summary>
    /// Determines whether the converter can convert the specified type.
    /// Only returns true if the type is an enum (or nullable enum) and contains at least one member with EnumFormatValueAttribute.
    /// </summary>
    /// <param name="typeToConvert">The type to check.</param>
    /// <returns>true if the type can be converted; otherwise, false.</returns>
    public override bool CanConvert(Type typeToConvert)
    {
        Type enumType;
        
        // Check if it's a nullable enum type
        if (!typeToConvert.IsEnum)
        {
            if (!(typeToConvert.IsGenericType && 
                typeToConvert.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                Nullable.GetUnderlyingType(typeToConvert)?.IsEnum == true))
            {
                return false;
            }
            
            enumType = Nullable.GetUnderlyingType(typeToConvert)!;
        }
        else
        {
            enumType = typeToConvert;
        }
        
        // Check if the enum type has any members with EnumFormatValueAttribute
        return HasEnumFormatValueAttribute(enumType);
    }

    /// <summary>
    /// Creates a converter for the specified type.
    /// </summary>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">The serialization options to use.</param>
    /// <returns>A converter for the specified type.</returns>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type converterType;
        
        // Handle nullable enums
        if (typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var underlyingType = Nullable.GetUnderlyingType(typeToConvert);
            converterType = typeof(NullableEnumFormatValueJsonConverter<>).MakeGenericType(underlyingType!);
        }
        else
        {
            converterType = typeof(EnumFormatValueJsonConverter<>).MakeGenericType(typeToConvert);
        }
        
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
    
    /// <summary>
    /// Determines whether the specified enum type has any members with EnumFormatValueAttribute.
    /// Uses caching to improve performance for repeated checks on the same type.
    /// </summary>
    /// <param name="enumType">The enum type to check.</param>
    /// <returns>true if the enum type has any members with EnumFormatValueAttribute; otherwise, false.</returns>
    private static bool HasEnumFormatValueAttribute(Type enumType)
    {
        return _hasAttributeCache.GetOrAdd(enumType, type =>
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetCustomAttribute<EnumFormatValueAttribute>() != null)
                {
                    return true;
                }
            }
            return false;
        });
    }
}

/// <summary>
/// JSON converter that handles nullable enum types with <see cref="EnumFormatValueAttribute"/>.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
public class NullableEnumFormatValueJsonConverter<TEnum> : JsonConverter<TEnum?> where TEnum : struct, Enum
{
    private readonly EnumFormatValueJsonConverter<TEnum> _underlyingConverter = new();
    
    /// <summary>
    /// Reads and converts the JSON to a nullable enum value using custom format values.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
            
        return _underlyingConverter.Read(ref reader, typeof(TEnum), options);
    }

    /// <summary>
    /// Writes a nullable enum value as JSON using custom format values.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
            
        _underlyingConverter.Write(writer, value.Value, options);
    }
} 