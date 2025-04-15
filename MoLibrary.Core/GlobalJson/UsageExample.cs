using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoLibrary.Core.GlobalJson.Attributes;
using MoLibrary.Repository.EFCoreExtensions;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MoLibrary.Core.GlobalJson;

/// <summary>
/// Example of how to use the EnumFormatValueAttribute.
/// </summary>
public static class EnumFormatValueUsage
{
    /// <summary>
    /// Example enum that uses EnumFormatValueAttribute.
    /// </summary>
    public enum ValidationStatus
    {
        /// <summary>
        /// Unknown validation status.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Validation passed.
        /// </summary>
        [EnumFormatValue("V")]
        Valid = 1,

        /// <summary>
        /// Validation failed.
        /// </summary>
        [EnumFormatValue("F")]
        Failed = 2,

        /// <summary>
        /// Validation is pending.
        /// </summary>
        [EnumFormatValue("P")]
        Pending = 3,

        /// <summary>
        /// Validation is in progress.
        /// </summary>
        [EnumFormatValue("I/P")]
        InProgress = 4,

        /// <summary>
        /// Validation not applicable.
        /// </summary>
        [EnumFormatValue("N/A")]
        NotApplicable = 5
    }

    /// <summary>
    /// Example entity that uses the enum.
    /// </summary>
    public class ValidationItem
    {
        /// <summary>
        /// The ID of the validation item.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The name of the validation item.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The status of the validation.
        /// This will be stored as "V", "F", etc. in the database.
        /// </summary>
        public ValidationStatus Status { get; set; }

        /// <summary>
        /// The optional extended status of the validation.
        /// This can be null, and will be stored as null, "V", "F", etc. in the database.
        /// </summary>
        public ValidationStatus? ExtendedStatus { get; set; }
    }

    /// <summary>
    /// Example entity configuration that configures the enum conversion.
    /// </summary>
    public class ValidationItemConfiguration : IEntityTypeConfiguration<ValidationItem>
    {
        /// <summary>
        /// Configures the entity type.
        /// </summary>
        /// <param name="builder">The entity type builder.</param>
        public void Configure(EntityTypeBuilder<ValidationItem> builder)
        {
            // Configure the Status property to use the EnumFormatValueConverter
            builder.Property(e => e.Status)
                .HasEnumFormatValueConversion()
                .HasMaxLength(10);

            // Configure the ExtendedStatus property to use the NullableEnumFormatValueConverter
            builder.Property(e => e.ExtendedStatus)
                .HasEnumFormatValueConversion()
                .HasMaxLength(10);
        }
    }

    /// <summary>
    /// Demonstrates JSON serialization and deserialization with EnumFormatValueAttribute.
    /// </summary>
    public static void JsonExample()
    {
        var item = new ValidationItem
        {
            Id = 1,
            Name = "Test Item",
            Status = ValidationStatus.Valid,
            ExtendedStatus = ValidationStatus.NotApplicable
        };

        // Serialize to JSON - will output "V" and "N/A" for the enums
        var json = JsonSerializer.Serialize(item, DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions);
        Console.WriteLine(json);

        // Deserialize from JSON - will convert "V" and "N/A" back to enum values
        var deserializedItem = JsonSerializer.Deserialize<ValidationItem>(json, DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions);
        Console.WriteLine($"Status: {deserializedItem?.Status}, ExtendedStatus: {deserializedItem?.ExtendedStatus}");

        // You can also parse formatted values directly
        var validationStatus = EnumFormatValueHelper.ParseFormattedValue<ValidationStatus>("V");
        Console.WriteLine($"Parsed status: {validationStatus}");
    }

    /// <summary>
    /// This class provides documentation on how to use the EnumFormatValueAttribute system.
    /// </summary>
    public static class Documentation
    {
        /// <summary>
        /// Information on how to use EnumFormatValueAttribute with JSON.
        /// </summary>
        public const string JsonUsage = @"
JSON Usage:
1. Mark your enum values with the [EnumFormatValue] attribute:
   
   public enum ValidationStatus
   {
       Unknown = 0,
       [EnumFormatValue(""V"")] Valid = 1,
       [EnumFormatValue(""F"")] Failed = 2
   }

2. The EnumFormatValueJsonConverterFactory is automatically registered in MoGlobalJsonExtensions.
   This will handle serialization and deserialization for all enums with EnumFormatValue attributes.

3. JSON serialization example:
   
   var item = new ValidationItem { Status = ValidationStatus.Valid };
   var json = JsonSerializer.Serialize(item, DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions);
   // The Status field will be serialized as ""V"" instead of ""Valid""
   
4. JSON deserialization example:
   
   var json = ""{ \""Status\"": \""V\"" }"";
   var item = JsonSerializer.Deserialize<ValidationItem>(json, DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions);
   // item.Status will be ValidationStatus.Valid
";

        /// <summary>
        /// Information on how to use EnumFormatValueAttribute with EF Core.
        /// </summary>
        public const string EfCoreUsage = @"
EF Core Usage:
1. Mark your enum values with the [EnumFormatValue] attribute:
   
   public enum ValidationStatus
   {
       Unknown = 0,
       [EnumFormatValue(""V"")] Valid = 1,
       [EnumFormatValue(""F"")] Failed = 2
   }

2. Configure the property in your entity configuration:
   
   public class ValidationItemConfiguration : IEntityTypeConfiguration<ValidationItem>
   {
       public void Configure(EntityTypeBuilder<ValidationItem> builder)
       {
           // For non-nullable enum
           builder.Property(e => e.Status)
               .HasEnumFormatValueConversion()
               .HasMaxLength(10);  // Set appropriate max length for the database column

           // For nullable enum
           builder.Property(e => e.ExtendedStatus)
               .HasEnumFormatValueConversion()
               .HasMaxLength(10);
       }
   }

3. The data will be stored in the database as ""V"", ""F"", etc. instead of integer values or enum names.
";

        /// <summary>
        /// Information on how to use EnumFormatValueHelper directly.
        /// </summary>
        public const string HelperUsage = @"
EnumFormatValueHelper Usage:
1. Get formatted value from enum:
   
   var status = ValidationStatus.Valid;
   string formatted = EnumFormatValueHelper.GetFormattedValue(status);
   // formatted will be ""V""

2. Parse formatted value to enum:
   
   ValidationStatus status = EnumFormatValueHelper.ParseFormattedValue<ValidationStatus>(""V"");
   // status will be ValidationStatus.Valid

3. Try to parse formatted value to enum:
   
   if (EnumFormatValueHelper.TryParseFormattedValue<ValidationStatus>(""V"", out var status))
   {
       // status is ValidationStatus.Valid
   }
";
    }
} 