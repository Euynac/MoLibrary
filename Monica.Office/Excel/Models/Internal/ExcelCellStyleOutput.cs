using System.Reflection;

namespace Monica.Office.Excel.Models.Internal
{
    /// <summary>
    /// Excel cell style information.
    /// </summary>
    internal class ExcelCellStyleOutput<TCellStyle, TStyle, TFont>(PropertyInfo propertyInfo, TCellStyle cellStyle, TStyle styleAttr, TFont fontAttr)
        where TStyle : Attribute
        where TFont : Attribute
    {
        /// <summary>
        /// Property mapped to the header cell.
        /// </summary>
        public PropertyInfo PropertyInfo { get; } = propertyInfo;

        /// <summary>
        /// Cell style.
        /// </summary>
        public TCellStyle CellStyle { get; } = cellStyle;

        /// <summary>
        /// Style attribute.
        /// </summary>
        public TStyle StyleAttr { get; } = styleAttr;

        /// <summary>
        /// Font attribute.
        /// </summary>
        public TFont FontAttr { get; } = fontAttr;
    }
}
