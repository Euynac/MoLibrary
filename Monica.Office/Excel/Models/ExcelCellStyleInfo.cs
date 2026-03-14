using System.Reflection;

namespace Monica.Office.Excel.Models
{
    /// <summary>
    /// excel 单元格样式信息
    /// </summary>
    public class ExcelCellStyleInfo<TStyle, TFont>(MemberInfo propertyInfo, TStyle styleAttr, TFont fontAttr)
        where TStyle : Attribute
        where TFont : Attribute
    {
        /// <summary>
        /// 表头对应的字段属性
        /// </summary>
        public MemberInfo PropertyInfo { get; } = propertyInfo;

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
