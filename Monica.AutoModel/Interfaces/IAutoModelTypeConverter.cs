using Monica.AutoModel.Configurations;
using Monica.AutoModel.Exceptions;
using Monica.AutoModel.Model;

namespace Monica.AutoModel.Interfaces;

/// <summary>
/// Converts raw filter values to typed AutoModel values.
/// </summary>
public interface IAutoModelTypeConverter
{
    /// <summary>
    /// Converts a raw field value.
    /// </summary>
    /// <param name="value">The raw string value.</param>
    /// <param name="typeSetting">The target field type metadata.</param>
    /// <param name="features">Additional condition features that affect conversion.</param>
    /// <exception cref="AutoModelValueConvertException">Thrown when the value cannot be converted.</exception>
    /// <returns>The converted value.</returns>
    dynamic? ConvertEntrance(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features);
}
