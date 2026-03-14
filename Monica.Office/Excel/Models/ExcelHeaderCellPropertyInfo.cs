using System.Reflection;

namespace Monica.Office.Excel.Models
{
    /// <summary>
    /// excel 表头单元格属性信息
    /// </summary>
    public class ExcelHeaderCellPropertyInfo : ExcelHeaderCellInfo<ExcelHeaderCellProperty>
    {
    }

    /// <summary>
    /// excel 表头单元格属性
    /// </summary>
    public class ExcelHeaderCellProperty : ExcelHeaderCell
    {
        /// <summary>
        /// 列属性信息
        /// </summary>
        public required PropertyInfo PropertyInfo { get; set; }
    }
}
