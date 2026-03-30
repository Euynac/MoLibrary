using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Monica.Office.Excel.Annotations;

namespace Monica.Office.Excel.Services.Support
{
    /// <summary>
    /// Resolves exportable and importable properties from Excel DTO types.
    /// </summary>
    internal static class ExcelPropertyResolver
    {
        /// <summary>
        /// Gets the DTO properties that participate in Excel import and export.
        /// </summary>
        /// <typeparam name="TDto">The DTO type.</typeparam>
        /// <returns></returns>
        public static PropertyInfo[] GetProperties<TDto>() where TDto : class
        {
            return typeof(TDto).GetProperties()
                .Where(property => property.GetCustomAttribute<IgnoreColumnAttribute>() == null)
                .ToArray();
        }

        /// <summary>
        /// Gets the display names used by the DTO properties in Excel headers.
        /// </summary>
        /// <typeparam name="TDto">The DTO type.</typeparam>
        /// <returns></returns>
        public static List<string> GetDisplayNames<TDto>() where TDto : class
        {
            return GetProperties<TDto>().Select(property => property.GetDisplayName()).ToList();
        }

        /// <summary>
        /// Gets the header display name for a property.
        /// </summary>
        /// <param name="property">The property to inspect.</param>
        /// <returns></returns>
        public static string GetDisplayName(this PropertyInfo property)
        {
            return property.GetCustomAttribute<DisplayAttribute>()?.Name ?? property.Name;
        }
    }
}
