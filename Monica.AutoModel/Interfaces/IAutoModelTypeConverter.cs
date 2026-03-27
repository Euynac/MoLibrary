using Monica.AutoModel.Configurations;
using Monica.AutoModel.Exceptions;
using Monica.AutoModel.Model;

namespace Monica.AutoModel.Interfaces;

/// <summary>
/// AutoModel type converter.
/// </summary>
public interface IAutoModelTypeConverter
{
    /// <summary>
    /// Converts a field value.
    /// </summary>
    /// <param name="value">The raw string value.</param>
    /// <param name="typeSetting">The field type settings.</param>
    /// <param name="features">Additional condition features that affect conversion.</param>
    /// <exception cref="AutoModelValueConvertException">Thrown when the value cannot be converted.</exception>
    /// <returns>The converted value.</returns>
    dynamic? ConvertEntrance(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features);
}
