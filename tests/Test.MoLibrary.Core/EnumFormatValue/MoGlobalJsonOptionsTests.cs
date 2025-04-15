using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.GlobalJson;
using MoLibrary.Core.GlobalJson.Attributes;
using MoLibrary.Core.GlobalJson.Converters;
using NUnit.Framework;

namespace Test.MoLibrary.Core.EnumFormatValue;

/// <summary>
/// Tests for the MoGlobalJsonOptions related to EnumFormatValue.
/// </summary>
[TestFixture]
public class MoGlobalJsonOptionsTests
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
        Failed = 2
    }

    /// <summary>
    /// Test that MoGlobalJsonOptions default values are set correctly.
    /// </summary>
    [Test]
    public void MoGlobalJsonOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new MoGlobalJsonOptions();

        // Assert
        options.EnableEnumFormatValue.Should().BeTrue();
    }

    /// <summary>
    /// Test that ConfigGlobalJsonSerializeOptions adds EnumFormatValueJsonConverterFactory when enabled.
    /// </summary>
    [Test]
    public void ConfigGlobalJsonSerializeOptions_WhenEnumFormatValueEnabled_ShouldAddConverter()
    {
        // Arrange
        var jsonOptions = new JsonSerializerOptions();
        var moOptions = new MoGlobalJsonOptions
        {
            EnableEnumFormatValue = true
        };

        // Act
        jsonOptions.ConfigGlobalJsonSerializeOptions(moOptions);

        // Assert
        jsonOptions.Converters.Should().Contain(c => c is EnumFormatValueJsonConverterFactory);
    }

    /// <summary>
    /// Test that ConfigGlobalJsonSerializeOptions does not add EnumFormatValueJsonConverterFactory when disabled.
    /// </summary>
    [Test]
    public void ConfigGlobalJsonSerializeOptions_WhenEnumFormatValueDisabled_ShouldNotAddConverter()
    {
        // Arrange
        var jsonOptions = new JsonSerializerOptions();
        var moOptions = new MoGlobalJsonOptions
        {
            EnableEnumFormatValue = false
        };

        // Act
        jsonOptions.ConfigGlobalJsonSerializeOptions(moOptions);

        // Assert
        jsonOptions.Converters.Should().NotContain(c => c is EnumFormatValueJsonConverterFactory);
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
    }

    /// <summary>
    /// Test that serialization works with EnumFormatValue when enabled.
    /// </summary>
    [Test]
    public void Serialization_WhenEnumFormatValueEnabled_ShouldUseFormattedValue()
    {
        // Arrange
        var jsonOptions = new JsonSerializerOptions();
        var moOptions = new MoGlobalJsonOptions
        {
            EnableEnumFormatValue = true
        };
        jsonOptions.ConfigGlobalJsonSerializeOptions(moOptions);

        var testObj = new TestClass { Status = TestEnum.Valid };

        // Act
        var json = JsonSerializer.Serialize(testObj, jsonOptions);

        // Assert
        json.Should().Contain("\"status\":\"V\"");
    }

    /// <summary>
    /// Test that serialization uses enum names with EnumFormatValue when disabled.
    /// </summary>
    [Test]
    public void Serialization_WhenEnumFormatValueDisabled_ShouldNotUseFormattedValue()
    {
        // Arrange
        var jsonOptions = new JsonSerializerOptions();
        var moOptions = new MoGlobalJsonOptions
        {
            EnableEnumFormatValue = false,
            EnableGlobalEnumToString = false // Disable both to get default System.Text.Json behavior
        };
        jsonOptions.ConfigGlobalJsonSerializeOptions(moOptions);

        var testObj = new TestClass { Status = TestEnum.Valid };

        // Act
        var json = JsonSerializer.Serialize(testObj, jsonOptions);

        // Assert
        json.Should().Contain("\"Status\":1"); // Default JSON serialization uses numeric values
    }

    /// <summary>
    /// Test that AddMoGlobalJsonSerialization correctly configures EnumFormatValue options.
    /// </summary>
    [Test]
    public void AddMoGlobalJsonSerialization_ShouldConfigureEnumFormatValueOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddMoGlobalJsonSerialization(options =>
        {
            options.EnableEnumFormatValue = true;
        });
        
        // Assert
        // Verify that DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions is configured correctly
        var jsonOptions = DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions;
        jsonOptions.Converters.Should().Contain(c => c is EnumFormatValueJsonConverterFactory);
    }
} 