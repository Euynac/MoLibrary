using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using OfficeOpenXml.Style;

namespace Monica.Office.Excel.Annotations
{
    /// <summary>
    /// Excel header font attribute (for export only)
    /// <para>1. Apply to classes and properties. It affects the header row only.</para>
    /// <para>2. If both the class and the property define it, the property-level setting takes precedence.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public sealed class HeaderFontAttribute : Attribute
    {
        /// <summary>
        /// Color index
        /// <para>NPOI: <see cref="IndexedColors"/> <see cref="HSSFColor"/>, for example: HSSFColor.Black.Index, IndexedColors.Black.Index</para>
        /// <para>EpPlus：<see cref="System.Drawing.Color"/></para>
        /// </summary>
        public short Color { get; set; } = -1;

        /// <summary>
        /// Font size in points
        /// <para>NPOI</para>
        /// <para>EpPlus</para>
        /// </summary>
        public short FontHeightInPoints { get; set; } = -1;

        /// <summary>
        /// Font name
        /// <para>NPOI</para>
        /// <para>EpPlus</para>
        /// </summary>
        public string? FontName { get; set; }
        /// <summary>
        /// Font height
        /// <para>NPOI</para>
        /// </summary>
        public double FontHeight { get; set; } = -1;
        /// <summary>
        /// Whether italic is enabled
        /// <para>NPOI</para>
        /// <para>EpPlus</para>
        /// </summary>
        public bool IsItalic { get; set; }
        /// <summary>
        /// Whether strikethrough is enabled
        /// <para>NPOI</para>
        /// <para>EpPlus</para>
        /// </summary>
        public bool IsStrikeout { get; set; }
        /// <summary>
        /// Superscript or subscript setting
        /// <para>NPOI：<see cref="FontSuperScript"/></para>
        /// </summary>
        public short TypeOffset { get; set; } = -1;
        /// <summary>
        /// Underline type
        /// <para>NPOI：<see cref="FontUnderlineType"/></para>
        /// <para>EpPlus：<see cref="ExcelUnderLineType"/></para>
        /// </summary>
        public short Underline { get; set; } = -1;
        /// <summary>
        /// Character set
        /// <para>NPOI</para>
        /// </summary>
        public short Charset { get; set; } = -1;
        /// <summary>
        /// Whether bold is enabled
        /// <para>NPOI</para>
        /// <para>EpPlus</para>
        /// </summary>
        public bool IsBold { get; set; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public HeaderFontAttribute()
        {
            IsBold = true;
        }
    }
}
