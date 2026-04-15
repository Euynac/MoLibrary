namespace Monica.WebApi.Swagger.Models;

/// <summary>
/// Describes where Swagger enum value descriptions should be loaded from.
/// </summary>
public enum ESwaggerEnumDescriptionSource
{
    /// <summary>
    /// Use <see cref="System.ComponentModel.DescriptionAttribute"/> on enum members.
    /// </summary>
    DescriptionAttribute,

    /// <summary>
    /// Use XML documentation comments.
    /// </summary>
    XmlComments,

    /// <summary>
    /// Use both sources, preferring <see cref="System.ComponentModel.DescriptionAttribute"/>.
    /// </summary>
    Both
}
