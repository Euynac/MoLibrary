using System.Text.Json;
using FluentAssertions;
using MoLibrary.Core.GlobalJson;
using MoLibrary.Core.GlobalJson.Attributes;
using MoLibrary.Core.GlobalJson.Converters;
using NUnit.Framework;

namespace Test.MoLibrary.Core.EnumFormatValue;

/// <summary>
/// Tests for the EnumFormatValueJsonConverter and related classes.
/// </summary>
[TestFixture]
public class EnumFormatValueJsonConverterTests
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
        ValidOrFailed = 3
    }

    /// <summary>
    /// Test class with enum properties.
    /// </summary>
    private class TestClass
    {
        /// <summary>
        /// Regular enum property.
        /// </summary>
        public TestEnum Status { get; set; }

        /// <summary>
        /// Nullable enum property.
        /// </summary>
        public TestEnum? NullableStatus { get; set; }
    }

    /// <summary>
    /// Test serialization of an enum with EnumFormatValueAttribute.
    /// </summary>
    [Test]
    public void Serialize_EnumWithEnumFormatValueAttribute_ShouldUseFormattedValue()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnumFormatValueJsonConverterFactory());

        var testObj = new TestClass
        {
            Status = TestEnum.Valid,
            NullableStatus = TestEnum.Failed
        };

        // Act
        var json = JsonSerializer.Serialize(testObj, options);

        // Assert
        json.Should().Contain("\"Status\":\"V\"");
        json.Should().Contain("\"NullableStatus\":\"F\"");
    }

    /// <summary>
    /// Test serialization of an enum with EnumFormatValueAttribute and a null nullable enum.
    /// </summary>
    [Test]
    public void Serialize_NullableEnumWithNull_ShouldSerializeAsNull()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnumFormatValueJsonConverterFactory());

        var testObj = new TestClass
        {
            Status = TestEnum.Valid,
            NullableStatus = null
        };

        // Act
        var json = JsonSerializer.Serialize(testObj, options);

        // Assert
        json.Should().Contain("\"Status\":\"V\"");
        json.Should().Contain("\"NullableStatus\":null");
    }

    /// <summary>
    /// Test serialization of an enum without EnumFormatValueAttribute.
    /// </summary>
    [Test]
    public void Serialize_EnumWithoutEnumFormatValueAttribute_ShouldUseEnumName()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnumFormatValueJsonConverterFactory());

        var testObj = new TestClass
        {
            Status = TestEnum.Unknown
        };

        // Act
        var json = JsonSerializer.Serialize(testObj, options);

        // Assert
        json.Should().Contain("\"Status\":\"Unknown\"");
    }

    /// <summary>
    /// Test deserialization of an enum with EnumFormatValueAttribute using formatted value.
    /// </summary>
    [Test]
    public void Deserialize_FormattedValue_ShouldResolveToCorrectEnumValue()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnumFormatValueJsonConverterFactory());

        var json = "{\"Status\":\"V\",\"NullableStatus\":\"F\"}";

        // Act
        var testObj = JsonSerializer.Deserialize<TestClass>(json, options);

        // Assert
        testObj.Should().NotBeNull();
        testObj!.Status.Should().Be(TestEnum.Valid);
        testObj.NullableStatus.Should().Be(TestEnum.Failed);
    }

    /// <summary>
    /// Test deserialization of an enum with EnumFormatValueAttribute using enum name.
    /// </summary>
    [Test]
    public void Deserialize_EnumName_ShouldResolveToCorrectEnumValue()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnumFormatValueJsonConverterFactory());

        var json = "{\"Status\":\"Valid\",\"NullableStatus\":\"Failed\"}";

        // Act
        var testObj = JsonSerializer.Deserialize<TestClass>(json, options);

        // Assert
        testObj.Should().NotBeNull();
        testObj!.Status.Should().Be(TestEnum.Valid);
        testObj.NullableStatus.Should().Be(TestEnum.Failed);
    }

    /// <summary>
    /// Test deserialization of an enum with EnumFormatValueAttribute using null for nullable enum.
    /// </summary>
    [Test]
    public void Deserialize_NullValue_ShouldResolveToNullForNullableEnum()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnumFormatValueJsonConverterFactory());

        var json = "{\"Status\":\"V\",\"NullableStatus\":null}";

        // Act
        var testObj = JsonSerializer.Deserialize<TestClass>(json, options);

        // Assert
        testObj.Should().NotBeNull();
        testObj!.Status.Should().Be(TestEnum.Valid);
        testObj.NullableStatus.Should().BeNull();
    }

    /// <summary>
    /// Test deserialization of an enum with EnumFormatValueAttribute using numeric value.
    /// </summary>
    [Test]
    public void Deserialize_NumericValue_ShouldResolveToCorrectEnumValue()
    {
        // Arrange
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EnumFormatValueJsonConverterFactory());

        var json = "{\"Status\":1,\"NullableStatus\":2}";

        // Act
        var testObj = JsonSerializer.Deserialize<TestClass>(json, options);

        // Assert
        testObj.Should().NotBeNull();
        testObj!.Status.Should().Be(TestEnum.Valid);
        testObj.NullableStatus.Should().Be(TestEnum.Failed);
    }

    /// <summary>
    /// Test EnumFormatValueJsonConverterFactory's CanConvert method.
    /// </summary>
    [Test]
    public void CanConvert_ShouldReturnCorrectResults()
    {
        // Arrange
        var factory = new EnumFormatValueJsonConverterFactory();

        // Act & Assert
        // Should convert enums
        factory.CanConvert(typeof(TestEnum)).Should().BeTrue();
        
        // Should convert nullable enums
        factory.CanConvert(typeof(TestEnum?)).Should().BeTrue();
        
        // Should not convert non-enums
        factory.CanConvert(typeof(string)).Should().BeFalse();
        factory.CanConvert(typeof(int)).Should().BeFalse();
        factory.CanConvert(typeof(int?)).Should().BeFalse();
        factory.CanConvert(typeof(object)).Should().BeFalse();
    }

    /// <summary>
    /// Test EnumFormatValueJsonConverterFactory's CreateConverter method.
    /// </summary>
    [Test]
    public void CreateConverter_ShouldCreateAppropriateConverter()
    {
        // Arrange
        var factory = new EnumFormatValueJsonConverterFactory();
        var options = new JsonSerializerOptions();

        // Act
        var enumConverter = factory.CreateConverter(typeof(TestEnum), options);
        var nullableEnumConverter = factory.CreateConverter(typeof(TestEnum?), options);

        // Assert
        enumConverter.Should().BeOfType<EnumFormatValueJsonConverter<TestEnum>>();
        nullableEnumConverter.Should().BeOfType<NullableEnumFormatValueJsonConverter<TestEnum>>();
    }
} 