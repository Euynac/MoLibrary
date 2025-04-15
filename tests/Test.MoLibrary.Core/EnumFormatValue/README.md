# EnumFormatValue System

This system allows you to set custom format values for enum fields, supporting special characters like "V/F" for enum values in JSON serialization/deserialization and database storage.

## Components

1. **EnumFormatValueAttribute**
   - Applied to enum fields to specify custom format values
   - Allows you to define how enum values should be represented in JSON and the database

2. **EnumFormatValueHelper**
   - Utility class for converting between enum values and their formatted string representations
   - Includes caching for better performance

3. **EnumFormatValueJsonConverter**
   - JSON converter that handles serialization/deserialization of enum values using their formatted values
   - Includes support for nullable enums

4. **EnumFormatValueConverter (EF Core)**
   - Entity Framework Core value converter for storing enum values as their formatted values in the database
   - Includes extension methods for easy configuration in entity type configurations

## Usage

### 1. Defining Enums with Format Values

```csharp
public enum ValidationStatus
{
    Unknown = 0,
    
    [EnumFormatValue("V")]
    Valid = 1,
    
    [EnumFormatValue("F")]
    Failed = 2,
    
    [EnumFormatValue("P")]
    Pending = 3,
    
    [EnumFormatValue("I/P")]
    InProgress = 4,
    
    [EnumFormatValue("N/A")]
    NotApplicable = 5
}
```

### 2. JSON Serialization

The EnumFormatValueJsonConverterFactory is automatically registered in MoGlobalJsonExtensions, so you don't need to configure it manually.

Example serialization:
```csharp
var item = new ValidationItem { Status = ValidationStatus.Valid };
var json = JsonSerializer.Serialize(item);
// The Status field will be serialized as "V" instead of "Valid"
```

Example deserialization:
```csharp
var json = "{ \"Status\": \"V\" }";
var item = JsonSerializer.Deserialize<ValidationItem>(json);
// item.Status will be ValidationStatus.Valid
```

### 3. Database Storage with EF Core

Configure your entity properties in the entity type configuration:

```csharp
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
```

The data will be stored in the database as "V", "F", etc. instead of integer values or enum names.

### 4. Configuration Options

You can configure the EnumFormatValue system in your application startup:

```csharp
services.AddMoGlobalJsonSerialization(options =>
{
    // Enable or disable EnumFormatValue (enabled by default)
    options.EnableEnumFormatValue = true;
});
```

## Testing

The EnumFormatValue system is thoroughly tested with unit tests located in the `MoLibrary/tests/Test.MoLibrary.Core/EnumFormatValue` directory. These tests use NUnit and FluentAssertions to provide comprehensive coverage of the feature:

1. **EnumFormatValueAttributeTests** - Tests for the attribute itself
2. **EnumFormatValueHelperTests** - Tests for the conversion helper methods
3. **EnumFormatValueJsonConverterTests** - Tests for JSON serialization/deserialization
4. **EnumFormatValueConverterTests** - Tests for EF Core database conversion
5. **MoGlobalJsonOptionsTests** - Tests for configuration options

You can run the tests using:

```
dotnet test MoLibrary/tests/Test.MoLibrary.Core/Test.MoLibrary.Core.csproj
```

## Advantages

1. **Readable Database Values**
   - Stores more readable values in the database like "V/F" instead of 0/1 or "Valid"/"Failed"
   - Easier for database administrators to understand the data

2. **Compact Representation**
   - Smaller string values reduce storage requirements and improve performance
   - "V" is more efficient than "Valid" for both storage and transmission

3. **Frontend Compatibility**
   - Supports frontend requirements for specific value formats
   - Ensures consistent data representation across systems

4. **Automatic Handling**
   - Converters handle serialization, deserialization, and database storage automatically
   - No need for manual conversion in your business code 