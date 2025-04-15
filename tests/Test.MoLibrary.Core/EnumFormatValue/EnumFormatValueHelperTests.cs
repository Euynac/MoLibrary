using FluentAssertions;
using MoLibrary.Core.GlobalJson;
using MoLibrary.Core.GlobalJson.Attributes;
using NUnit.Framework;

namespace Test.MoLibrary.Core.EnumFormatValue;

/// <summary>
/// Tests for the EnumFormatValueHelper class.
/// </summary>
[TestFixture]
public class EnumFormatValueHelperTests
{
    /// <summary>
    /// Test enum that uses EnumFormatValueAttribute.
    /// </summary>
    public enum TestEnum
    {
        /// <summary>
        /// Unknown value without format attribute.
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
        ValidOrFailed = 3,

        /// <summary>
        /// Not applicable with "N/A" format.
        /// </summary>
        [EnumFormatValue("N/A")]
        NotApplicable = 4
    }

    /// <summary>
    /// Test that GetFormattedValue returns the correct formatted value for an enum value.
    /// </summary>
    [Test]
    public void GetFormattedValue_ShouldReturnCorrectFormattedValue()
    {
        // Arrange & Act & Assert
        EnumFormatValueHelper.GetFormattedValue(TestEnum.Unknown).Should().Be("Unknown");
        EnumFormatValueHelper.GetFormattedValue(TestEnum.Valid).Should().Be("V");
        EnumFormatValueHelper.GetFormattedValue(TestEnum.Failed).Should().Be("F");
        EnumFormatValueHelper.GetFormattedValue(TestEnum.ValidOrFailed).Should().Be("V/F");
        EnumFormatValueHelper.GetFormattedValue(TestEnum.NotApplicable).Should().Be("N/A");
    }

    /// <summary>
    /// Test that GetFormattedValue caches values correctly.
    /// </summary>
    [Test]
    public void GetFormattedValue_ShouldCacheValues()
    {
        // First call should cache the value
        var firstCall = EnumFormatValueHelper.GetFormattedValue(TestEnum.Valid);
        
        // Second call should retrieve from cache
        var secondCall = EnumFormatValueHelper.GetFormattedValue(TestEnum.Valid);
        
        firstCall.Should().Be("V");
        secondCall.Should().Be("V");
    }

    /// <summary>
    /// Test that GetFormattedValue throws ArgumentNullException for null enum value.
    /// </summary>
    [Test]
    public void GetFormattedValue_ShouldThrowOnNullEnum()
    {
        // Act & Assert
        var action = () => EnumFormatValueHelper.GetFormattedValue(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Test ParseFormattedValue with valid format value.
    /// </summary>
    [Test]
    public void ParseFormattedValue_WithValidFormatValue_ShouldReturnCorrectEnumValue()
    {
        // Arrange & Act
        var valid = EnumFormatValueHelper.ParseFormattedValue<TestEnum>("V");
        var failed = EnumFormatValueHelper.ParseFormattedValue<TestEnum>("F");
        var validOrFailed = EnumFormatValueHelper.ParseFormattedValue<TestEnum>("V/F");
        var notApplicable = EnumFormatValueHelper.ParseFormattedValue<TestEnum>("N/A");

        // Assert
        valid.Should().Be(TestEnum.Valid);
        failed.Should().Be(TestEnum.Failed);
        validOrFailed.Should().Be(TestEnum.ValidOrFailed);
        notApplicable.Should().Be(TestEnum.NotApplicable);
    }

    /// <summary>
    /// Test ParseFormattedValue with enum name instead of formatted value.
    /// </summary>
    [Test]
    public void ParseFormattedValue_WithEnumName_ShouldReturnCorrectEnumValue()
    {
        // Arrange & Act
        var unknown = EnumFormatValueHelper.ParseFormattedValue<TestEnum>("Unknown");
        var valid = EnumFormatValueHelper.ParseFormattedValue<TestEnum>("Valid");

        // Assert
        unknown.Should().Be(TestEnum.Unknown);
        valid.Should().Be(TestEnum.Valid);
    }

    /// <summary>
    /// Test ParseFormattedValue with invalid value throws exception.
    /// </summary>
    [Test]
    public void ParseFormattedValue_WithInvalidValue_ShouldThrowArgumentException()
    {
        // Act & Assert
        var action = () => EnumFormatValueHelper.ParseFormattedValue<TestEnum>("InvalidValue");
        action.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Test TryParseFormattedValue with valid format value.
    /// </summary>
    [Test]
    public void TryParseFormattedValue_WithValidFormatValue_ShouldReturnTrueAndCorrectEnum()
    {
        // Arrange
        TestEnum valid;
        TestEnum failed;
        TestEnum validOrFailed;

        // Act
        var validResult = EnumFormatValueHelper.TryParseFormattedValue("V", out valid);
        var failedResult = EnumFormatValueHelper.TryParseFormattedValue("F", out failed);
        var validOrFailedResult = EnumFormatValueHelper.TryParseFormattedValue("V/F", out validOrFailed);

        // Assert
        validResult.Should().BeTrue();
        valid.Should().Be(TestEnum.Valid);

        failedResult.Should().BeTrue();
        failed.Should().Be(TestEnum.Failed);

        validOrFailedResult.Should().BeTrue();
        validOrFailed.Should().Be(TestEnum.ValidOrFailed);
    }

    /// <summary>
    /// Test TryParseFormattedValue with invalid value.
    /// </summary>
    [Test]
    public void TryParseFormattedValue_WithInvalidValue_ShouldReturnFalse()
    {
        // Arrange
        TestEnum result;

        // Act
        var success = EnumFormatValueHelper.TryParseFormattedValue("InvalidValue", out result);

        // Assert
        success.Should().BeFalse();
        result.Should().Be(default);
    }

    /// <summary>
    /// Test TryParseFormattedValue with null or empty value.
    /// </summary>
    [Test]
    public void TryParseFormattedValue_WithNullOrEmptyValue_ShouldReturnFalse()
    {
        // Arrange
        TestEnum result1;
        TestEnum result2;

        // Act
        var nullResult = EnumFormatValueHelper.TryParseFormattedValue(null, out result1);
        var emptyResult = EnumFormatValueHelper.TryParseFormattedValue("", out result2);

        // Assert
        nullResult.Should().BeFalse();
        result1.Should().Be(default);

        emptyResult.Should().BeFalse();
        result2.Should().Be(default);
    }

    /// <summary>
    /// Test that TryParseFormattedValue caches values correctly.
    /// </summary>
    [Test]
    public void TryParseFormattedValue_ShouldCacheValues()
    {
        // First call should cache the value
        EnumFormatValueHelper.TryParseFormattedValue("V", out TestEnum firstResult);
        
        // Second call should retrieve from cache
        EnumFormatValueHelper.TryParseFormattedValue("V", out TestEnum secondResult);
        
        firstResult.Should().Be(TestEnum.Valid);
        secondResult.Should().Be(TestEnum.Valid);
    }
} 