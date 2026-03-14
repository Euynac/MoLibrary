using System.Reflection;

namespace Monica.Office.Excel.Models
{
    /// <summary>
    /// excel 单元格样式信息
    /// </summary>
    public class ExcelCellStyleOutput<TCellStyle, TStyle, TFont>(PropertyInfo propertyInfo, TCellStyle cellStyle, TStyle styleAttr, TFont fontAttr)
        where TStyle : Attribute
        where TFont : Attribute
    {
        /// <summary>
        /// 表头对应的字段属性
        /// </summary>
        public PropertyInfo PropertyInfo { get; } = propertyInfo;

        /// <summary>
        /// 单元格样式
        /// </summary>
        public TCellStyle CellStyle { get; } = cellStyle;

        /// <summary>
        /// 样式
        /// </summary>
        public TStyle StyleAttr { get; } = styleAttr;

        /// <summary>
        /// 字体
        /// </summary>
        public TFont FontAttr { get; } = fontAttr;
    }
}
