using System.Reflection;

namespace Monica.Office.Excel.Models.Internal
{
    /// <summary>
    /// Excel header cell property information.
    /// </summary>
    internal class ExcelHeaderCellPropertyInfo : ExcelHeaderCellInfo<ExcelHeaderCellProperty>
    {
    }

    /// <summary>
    /// Excel header cell property.
    /// </summary>
    internal class ExcelHeaderCellProperty : ExcelHeaderCell
    {
        /// <summary>
        /// Column property information.
        /// </summary>
        public required PropertyInfo PropertyInfo { get; set; }
    }
}
