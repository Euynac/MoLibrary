using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MoLibrary.Core.GlobalJson;

namespace MoLibrary.Repository.EFCoreExtensions;

/// <summary>
/// EF Core ValueConverter that converts enums to their formatted values in the database and back.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
public class EnumFormatValueConverter<TEnum> : ValueConverter<TEnum, string> where TEnum : struct, Enum
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnumFormatValueConverter{TEnum}"/> class.
    /// </summary>
    /// <param name="mappingHints">The mapping hints to use when creating column mappings.</param>
    public EnumFormatValueConverter(ConverterMappingHints? mappingHints = null)
        : base(
            // Convert to database
            e => EnumFormatValueHelper.GetFormattedValue(e),
            // Convert from database
            s => EnumFormatValueHelper.ParseFormattedValue<TEnum>(s),
            mappingHints)
    {
    }
}

/// <summary>
/// EF Core ValueConverter that converts nullable enums to their formatted values in the database and back.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
public class NullableEnumFormatValueConverter<TEnum> : ValueConverter<TEnum?, string?> where TEnum : struct, Enum
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NullableEnumFormatValueConverter{TEnum}"/> class.
    /// </summary>
    /// <param name="mappingHints">The mapping hints to use when creating column mappings.</param>
    public NullableEnumFormatValueConverter(ConverterMappingHints? mappingHints = null)
        : base(
            // Convert to database
            e => e == null ? null : EnumFormatValueHelper.GetFormattedValue(e.Value),
            // Convert from database
            s => string.IsNullOrEmpty(s) ? null : EnumFormatValueHelper.ParseFormattedValue<TEnum>(s),
            mappingHints)
    {
    }
}

/// <summary>
/// Extension methods for setting up enum format value conversions in EF Core models.
/// </summary>
public static class EnumFormatValueConverterExtensions
{
    /// <summary>
    /// Configures the property to use the EnumFormatValueConverter.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="propertyBuilder">The property builder.</param>
    /// <returns>The property builder for method chaining.</returns>
    public static Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<TEnum> HasEnumFormatValueConversion<TEnum>(
        this Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<TEnum> propertyBuilder) 
        where TEnum : struct, Enum
    {
        return propertyBuilder.HasConversion(new EnumFormatValueConverter<TEnum>());
    }

    /// <summary>
    /// Configures the property to use the NullableEnumFormatValueConverter.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="propertyBuilder">The property builder.</param>
    /// <returns>The property builder for method chaining.</returns>
    public static Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<TEnum?> HasEnumFormatValueConversion<TEnum>(
        this Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<TEnum?> propertyBuilder) 
        where TEnum : struct, Enum
    {
        return propertyBuilder.HasConversion(new NullableEnumFormatValueConverter<TEnum>());
    }
} 