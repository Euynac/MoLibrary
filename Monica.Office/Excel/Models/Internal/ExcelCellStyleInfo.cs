using System.Reflection;

namespace Monica.Office.Excel.Models.Internal
{
    /// <summary>
    /// Excel cell style information.
    /// </summary>
    internal class ExcelCellStyleInfo<TStyle, TFont>(MemberInfo propertyInfo, TStyle styleAttr, TFont fontAttr)
        where TStyle : Attribute
        where TFont : Attribute
    {
        /// <summary>
        /// Property mapped to the header cell.
        /// </summary>
        public MemberInfo PropertyInfo { get; } = propertyInfo;

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
