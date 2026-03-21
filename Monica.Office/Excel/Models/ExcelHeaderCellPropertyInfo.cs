using System.Reflection;

namespace Monica.Office.Excel.Models
{
    /// <summary>
    /// Excel header cell property information.
    /// </summary>
    public class ExcelHeaderCellPropertyInfo : ExcelHeaderCellInfo<ExcelHeaderCellProperty>
    {
    }

    /// <summary>
    /// Excel header cell property.
    /// </summary>
    public class ExcelHeaderCellProperty : ExcelHeaderCell
    {
        /// <summary>
        /// Column property information.
        /// </summary>
        public required PropertyInfo PropertyInfo { get; set; }
    }
}
