using System.Reflection;
using FluentAssertions;
using MoLibrary.Core.GlobalJson.Attributes;
using NUnit.Framework;

namespace Test.MoLibrary.Core.EnumFormatValue;

/// <summary>
/// Tests for the EnumFormatValueAttribute class.
/// </summary>
[TestFixture]
public class EnumFormatValueAttributeTests
{
    /// <summary>
    /// Test enum that uses EnumFormatValueAttribute.
    /// </summary>
    public enum TestEnum
    {
        /// <summary>
        /// Unknown value.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Valid value with "V" format.
        /// </summary>
        [EnumFormatValue("V")]
        Valid = 1,

        /// <summary>
        /// Failed value with "F" format.
        /// </summary>
        [EnumFormatValue("F")]
        Failed = 2,

        /// <summary>
        /// Value with special characters in the format.
        /// </summary>
        [EnumFormatValue("V/F")]
        ValidOrFailed = 3
    }

    /// <summary>
    /// Verify that EnumFormatValueAttribute is applied to enum fields correctly.
    /// </summary>
    [Test]
    public void EnumFormatValueAttribute_ShouldBeAppliedToEnumFields()
    {
        // Arrange
        var validField = typeof(TestEnum).GetField(nameof(TestEnum.Valid));
        var failedField = typeof(TestEnum).GetField(nameof(TestEnum.Failed));
        var validOrFailedField = typeof(TestEnum).GetField(nameof(TestEnum.ValidOrFailed));
        var unknownField = typeof(TestEnum).GetField(nameof(TestEnum.Unknown));

        // Act
        var validAttribute = validField?.GetCustomAttribute<EnumFormatValueAttribute>();
        var failedAttribute = failedField?.GetCustomAttribute<EnumFormatValueAttribute>();
        var validOrFailedAttribute = validOrFailedField?.GetCustomAttribute<EnumFormatValueAttribute>();
        var unknownAttribute = unknownField?.GetCustomAttribute<EnumFormatValueAttribute>();

        // Assert
        validAttribute.Should().NotBeNull();
        validAttribute?.FormattedValue.Should().Be("V");

        failedAttribute.Should().NotBeNull();
        failedAttribute?.FormattedValue.Should().Be("F");

        validOrFailedAttribute.Should().NotBeNull();
        validOrFailedAttribute?.FormattedValue.Should().Be("V/F");

        unknownAttribute.Should().BeNull(); // No attribute applied to the Unknown field
    }

    /// <summary>
    /// Verify that constructor throws on null formatted value.
    /// </summary>
    [Test]
    public void EnumFormatValueAttribute_ShouldThrowOnNullFormattedValue()
    {
        // Act & Assert
        var action = () => new EnumFormatValueAttribute(null!);
        action.Should().Throw<ArgumentNullException>();
    }
} 