namespace MoLibrary.Core.GlobalJson.Attributes;

/// <summary>
/// Specifies a custom format value for an enum field that will be used during serialization and deserialization.
/// Supports special characters like "V/F" for enum values in JSON and database operations.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class EnumFormatValueAttribute : Attribute
{
    /// <summary>
    /// Gets the formatted value to use for this enum field.
    /// </summary>
    public string FormattedValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnumFormatValueAttribute"/> class with a specified formatted value.
    /// </summary>
    /// <param name="formattedValue">The formatted value to use for the enum field.</param>
    public EnumFormatValueAttribute(string formattedValue)
    {
        FormattedValue = formattedValue ?? throw new ArgumentNullException(nameof(formattedValue));
    }
} 