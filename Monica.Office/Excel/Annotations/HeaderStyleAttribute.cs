using System.ComponentModel.DataAnnotations;
using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using OfficeOpenXml.Style;

namespace Monica.Office.Excel.Annotations
{
    /// <summary>
    /// Excel header style attribute (for export only)
    /// <para>1. Apply to classes and properties. It affects the header row only.</para>
    /// <para>2. If both the class and the property define it, the property-level setting takes precedence.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public sealed class HeaderStyleAttribute : Attribute
    {
        /// <summary>
        /// Shrink to fit
        /// <para>Npoi</para>
        /// <para>EpPlus</para>
        /// </summary>
        public bool ShrinkToFit { get; set; }

        /// <summary>
        /// Whether the column width is adjusted automatically. Default: false.
        /// <para>If <see cref="ColumnSize"/> &gt; 0, <see cref="ColumnSize"/> takes effect.</para>
        /// <para>Npoi</para>
        /// <para>EpPlus</para>
        /// </summary>
        public bool ColumnAutoSize { get; set; }

        /// <summary>
        /// Column width
        /// <para>Unit: characters.</para>
        /// <para>Range: [0-255].</para>
        /// <para>Npoi</para>
        /// <para>EpPlus</para>
        /// </summary>
        [Range(0, 255)]
        public int ColumnSize { get; set; } = -1;

        /// <summary>
        /// Data format
        /// <para>Npoi</para>
        /// <para>EpPlus</para>
        /// </summary>
        public string? DataFormat { get; set; }

        /// <summary>
        /// Whether text wraps automatically
        /// <para>Npoi</para>
        /// <para>EpPlus</para>
        /// </summary>
        public bool WrapText { get; set; }

        /// <summary>
        /// Indentation
        /// <para>Npoi</para>
        /// <para>EpPlus</para>
        /// </summary>
        public short Indention { get; set; } = -1;

        /// <summary>
        /// Horizontal alignment
        /// <para>NPOI：<see cref="HorizontalAlignment"/></para>
        /// <para>EpPlus：<see cref="ExcelHorizontalAlignment"/></para>
        /// </summary>
        public short Alignment { get; set; }

        /// <summary>
        /// Vertical alignment
        /// <para>NPOI：<see cref="NPOI.SS.UserModel.VerticalAlignment"/></para>
        /// <para>EpPlus：<see cref="ExcelVerticalAlignment"/></para>
        /// </summary>
        public short VerticalAlignment { get; set; }

        /// <summary>
        /// Whether the cell is hidden
        /// <para>Npoi</para>
        /// <para>EpPlus</para>
        /// </summary>
        public bool IsHidden { get; set; }

        /// <summary>
        /// Whether the cell is locked
        /// <para>Npoi</para>
        /// <para>EpPlus</para>
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>
        /// Rotation
        /// <para>Npoi</para>
        /// <para>EpPlus</para>
        /// </summary>
        public short Rotation { get; set; } = -1;

        /// <summary>
        /// Left border
        /// <para>NPOI：<see cref="BorderStyle"/></para>
        /// <para>EpLus：<see cref="ExcelBorderStyle"/></para>
        /// </summary>
        public short BorderLeft { get; set; } = -1;

        /// <summary>
        /// Right border
        /// <para>NPOI：<see cref="BorderStyle"/></para>
        /// <para>EpLus：<see cref="ExcelBorderStyle"/></para>
        /// </summary>
        public short BorderRight { get; set; } = -1;

        /// <summary>
        /// Top border
        /// <para>NPOI：<see cref="BorderStyle"/></para>
        /// <para>EpLus：<see cref="ExcelBorderStyle"/></para>
        /// </summary>
        public short BorderTop { get; set; } = -1;

        /// <summary>
        /// Bottom border
        /// <para>NPOI：<see cref="BorderStyle"/></para>
        /// <para>EpLus：<see cref="ExcelBorderStyle"/></para>
        /// </summary>
        public short BorderBottom { get; set; } = -1;

        /// <summary>
        /// Left border color
        /// <para>NPOI: <see cref="IndexedColors"/> <see cref="HSSFColor"/>, for example: HSSFColor.Black.Index, IndexedColors.Black.Index</para>
        /// <para>EpPlus：<see cref="System.Drawing.Color"/></para>
        /// </summary>
        public short LeftBorderColor { get; set; } = -1;

        /// <summary>
        /// Right border color
        /// <para>NPOI: <see cref="IndexedColors"/> <see cref="HSSFColor"/>, for example: HSSFColor.Black.Index, IndexedColors.Black.Index</para>
        /// <para>EpPlus：<see cref="System.Drawing.Color"/></para>
        /// </summary>
        public short RightBorderColor { get; set; } = -1;

        /// <summary>
        /// Top border color
        /// <para>NPOI: <see cref="IndexedColors"/> <see cref="HSSFColor"/>, for example: HSSFColor.Black.Index, IndexedColors.Black.Index</para>
        /// <para>EpPlus：<see cref="System.Drawing.Color"/></para>
        /// </summary>
        public short TopBorderColor { get; set; } = -1;

        /// <summary>
        /// Bottom border color
        /// <para>NPOI: <see cref="IndexedColors"/> <see cref="HSSFColor"/>, for example: HSSFColor.Black.Index, IndexedColors.Black.Index</para>
        /// <para>EpPlus：<see cref="System.Drawing.Color"/></para>
        /// </summary>
        public short BottomBorderColor { get; set; } = -1;
        /// <summary>
        /// Fill pattern
        /// <para>NPOI：<see cref="NPOI.SS.UserModel.FillPattern"/></para>
        /// <para>EpPlus：<see cref="ExcelFillStyle"/></para>
        /// </summary>
        public short FillPattern { get; set; } = -1;

        /// <summary>
        /// Fill background color
        /// <para>NPOI: <see cref="IndexedColors"/> <see cref="HSSFColor"/>, for example: HSSFColor.Black.Index, IndexedColors.Black.Index</para>
        /// <para>EpPlus：<see cref="System.Drawing.Color"/></para>
        /// </summary>
        public short FillBackgroundColor { get; set; } = -1;

        /// <summary>
        /// Fill foreground color
        /// <para>NPOI: <see cref="IndexedColors"/> <see cref="HSSFColor"/>, for example: HSSFColor.Black.Index, IndexedColors.Black.Index</para>
        /// </summary>
        public short FillForegroundColor { get; set; } = -1;

        /// <summary>
        /// Diagonal border color
        /// <para>NPOI: <see cref="IndexedColors"/> <see cref="HSSFColor"/>, for example: HSSFColor.Black.Index, IndexedColors.Black.Index</para>
        /// <para>EpPlus：<see cref="System.Drawing.Color"/></para>
        /// </summary>
        public short BorderDiagonalColor { get; set; } = -1;

        /// <summary>
        /// Diagonal border line style
        /// <para>NPOI：<see cref="BorderStyle"/></para>
        /// <para>EpPlus：<see cref="ExcelBorderStyle"/></para>
        /// </summary>
        public short BorderDiagonalLineStyle { get; set; } = -1;

        /// <summary>
        /// Diagonal border
        /// <para>NPOI：<see cref="NPOI.SS.UserModel.BorderDiagonal"/></para>
        /// </summary>
        public int BorderDiagonal { get; set; } = -1;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public HeaderStyleAttribute()
        {
            IsLocked = true;
            Alignment = 2;
            VerticalAlignment = 1;
        }
    }
}
