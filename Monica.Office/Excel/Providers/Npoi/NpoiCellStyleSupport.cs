using Monica.Office.Excel.Annotations;
using NPOI.SS.UserModel;

namespace Monica.Office.Excel.Providers.Npoi
{
    /// <summary>
    /// NPOI cell style support.
    /// </summary>
    internal class NpoiCellStyleSupport : INpoiCellStyleSupport
    {
        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public NpoiCellStyleSupport()
        {
        }

        /// <summary>
        /// Applies the header cell style and font.
        /// </summary>
        /// <param name="workbook"></param>
        /// <param name="fontAttr"></param>
        /// <param name="styleAttr"></param>
        /// <returns></returns>
        public virtual ICellStyle SetHeaderCellStyleAndFont(IWorkbook workbook, HeaderStyleAttribute styleAttr, HeaderFontAttribute fontAttr)
        {
            // Default header cell style
            var defaultStyle = CreateHeaderCellStyle(workbook, styleAttr);
            defaultStyle.SetFont(CreateHeaderCellFont(workbook, fontAttr));
            return defaultStyle;
        }

        /// <summary>
        /// Applies the data cell style and font.
        /// </summary>
        /// <param name="workbook"></param>
        /// <param name="styleAttr"></param>
        /// <param name="fontAttr"></param>
        /// <returns></returns>

        public virtual ICellStyle SetDataCellStyleAndFont(IWorkbook workbook, DataStyleAttribute styleAttr, DataFontAttribute fontAttr)
        {
            // Default data cell style
            var defaultStyle = CreateDataCellStyle(workbook, styleAttr);
            defaultStyle.SetFont(CreateDataCellFont(workbook, fontAttr));
            return defaultStyle;
        }

        /// <summary>
        /// Creates the header cell style.
        /// </summary>
        public virtual ICellStyle CreateHeaderCellStyle(IWorkbook workbook, HeaderStyleAttribute? styleAttr)
        {
            var cellStyle = workbook.CreateCellStyle();

            styleAttr = styleAttr ?? new HeaderStyleAttribute();

            if (!string.IsNullOrWhiteSpace(styleAttr.DataFormat)) cellStyle.DataFormat = workbook.CreateDataFormat().GetFormat(styleAttr.DataFormat);
            cellStyle.ShrinkToFit = styleAttr.ShrinkToFit;
            cellStyle.IsHidden = styleAttr.IsHidden;
            cellStyle.IsLocked = styleAttr.IsLocked;
            cellStyle.WrapText = styleAttr.WrapText;

            if (styleAttr.Indention > -1) cellStyle.Indention = styleAttr.Indention;
            if (styleAttr.Rotation > -1) cellStyle.Rotation = styleAttr.Rotation;
            if (styleAttr.BorderLeft > -1) cellStyle.BorderLeft = (BorderStyle)styleAttr.BorderLeft;
            if (styleAttr.BorderRight > -1) cellStyle.BorderRight = (BorderStyle)styleAttr.BorderRight;
            if (styleAttr.BorderTop > -1) cellStyle.BorderTop = (BorderStyle)styleAttr.BorderTop;
            if (styleAttr.BorderBottom > -1) cellStyle.BorderBottom = (BorderStyle)styleAttr.BorderBottom;
            if (styleAttr.LeftBorderColor > -1) cellStyle.LeftBorderColor = styleAttr.LeftBorderColor;
            if (styleAttr.RightBorderColor > -1) cellStyle.RightBorderColor = styleAttr.RightBorderColor;
            if (styleAttr.TopBorderColor > -1) cellStyle.TopBorderColor = styleAttr.TopBorderColor;
            if (styleAttr.BottomBorderColor > -1) cellStyle.BottomBorderColor = styleAttr.BottomBorderColor;
            if (styleAttr.FillPattern > -1) cellStyle.FillPattern = (FillPattern)styleAttr.FillPattern;
            if (styleAttr.FillBackgroundColor > -1) cellStyle.FillBackgroundColor = styleAttr.FillBackgroundColor;
            if (styleAttr.FillForegroundColor > -1) cellStyle.FillForegroundColor = styleAttr.FillForegroundColor;
            if (styleAttr.BorderDiagonalColor > -1) cellStyle.BorderDiagonalColor = styleAttr.BorderDiagonalColor;
            if (styleAttr.BorderDiagonalLineStyle > -1) cellStyle.BorderDiagonalLineStyle = (BorderStyle)styleAttr.BorderDiagonalLineStyle;
            if (styleAttr.BorderDiagonal > -1) cellStyle.BorderDiagonal = (BorderDiagonal)styleAttr.BorderDiagonal;
            if (styleAttr.Alignment > -1) cellStyle.Alignment = (HorizontalAlignment)styleAttr.Alignment;
            if (styleAttr.VerticalAlignment > -1) cellStyle.VerticalAlignment = (VerticalAlignment)styleAttr.VerticalAlignment;

            return cellStyle;
        }

        /// <summary>
        /// Creates the header cell font.
        /// </summary>
        public virtual IFont CreateHeaderCellFont(IWorkbook workbook, HeaderFontAttribute? fontAttr)
        {
            var font = workbook.CreateFont();

            fontAttr = fontAttr ?? new HeaderFontAttribute();

            font.FontName = fontAttr.FontName;
            font.IsItalic = fontAttr.IsItalic;
            font.IsStrikeout = fontAttr.IsStrikeout;
            font.IsBold = fontAttr.IsBold;

            if (fontAttr.Color > -1) font.Color = fontAttr.Color;
            if (fontAttr.FontHeight > -1) font.FontHeight = fontAttr.FontHeight;
            if (fontAttr.TypeOffset > -1) font.TypeOffset = (FontSuperScript)fontAttr.TypeOffset;
            if (fontAttr.Underline > -1) font.Underline = (FontUnderlineType)fontAttr.Underline;
            if (fontAttr.FontHeightInPoints > -1) font.FontHeightInPoints = fontAttr.FontHeightInPoints;
            if (fontAttr.Charset > -1) font.Charset = fontAttr.Charset;

            return font;
        }

        /// <summary>
        /// Creates the data cell style.
        /// </summary>
        public virtual ICellStyle CreateDataCellStyle(IWorkbook workbook, DataStyleAttribute? styleAttr)
        {
            var cellStyle = workbook.CreateCellStyle();

            styleAttr ??= new DataStyleAttribute();

            if (!string.IsNullOrWhiteSpace(styleAttr.DataFormat)) cellStyle.DataFormat = workbook.CreateDataFormat().GetFormat(styleAttr.DataFormat);
            cellStyle.ShrinkToFit = styleAttr.ShrinkToFit;
            cellStyle.IsHidden = styleAttr.IsHidden;
            cellStyle.IsLocked = styleAttr.IsLocked;
            cellStyle.WrapText = styleAttr.WrapText;

            if (styleAttr.Indention > -1) cellStyle.Indention = styleAttr.Indention;
            if (styleAttr.Rotation > -1) cellStyle.Rotation = styleAttr.Rotation;
            if (styleAttr.BorderLeft > -1) cellStyle.BorderLeft = (BorderStyle)styleAttr.BorderLeft;
            if (styleAttr.BorderRight > -1) cellStyle.BorderRight = (BorderStyle)styleAttr.BorderRight;
            if (styleAttr.BorderTop > -1) cellStyle.BorderTop = (BorderStyle)styleAttr.BorderTop;
            if (styleAttr.BorderBottom > -1) cellStyle.BorderBottom = (BorderStyle)styleAttr.BorderBottom;
            if (styleAttr.LeftBorderColor > -1) cellStyle.LeftBorderColor = styleAttr.LeftBorderColor;
            if (styleAttr.RightBorderColor > -1) cellStyle.RightBorderColor = styleAttr.RightBorderColor;
            if (styleAttr.TopBorderColor > -1) cellStyle.TopBorderColor = styleAttr.TopBorderColor;
            if (styleAttr.BottomBorderColor > -1) cellStyle.BottomBorderColor = styleAttr.BottomBorderColor;
            if (styleAttr.FillPattern > -1) cellStyle.FillPattern = (FillPattern)styleAttr.FillPattern;
            if (styleAttr.FillBackgroundColor > -1) cellStyle.FillBackgroundColor = styleAttr.FillBackgroundColor;
            if (styleAttr.FillForegroundColor > -1) cellStyle.FillForegroundColor = styleAttr.FillForegroundColor;
            if (styleAttr.BorderDiagonalColor > -1) cellStyle.BorderDiagonalColor = styleAttr.BorderDiagonalColor;
            if (styleAttr.BorderDiagonalLineStyle > -1) cellStyle.BorderDiagonalLineStyle = (BorderStyle)styleAttr.BorderDiagonalLineStyle;
            if (styleAttr.BorderDiagonal > -1) cellStyle.BorderDiagonal = (BorderDiagonal)styleAttr.BorderDiagonal;
            if (styleAttr.Alignment > -1) cellStyle.Alignment = (HorizontalAlignment)styleAttr.Alignment;
            if (styleAttr.VerticalAlignment > -1) cellStyle.VerticalAlignment = (VerticalAlignment)styleAttr.VerticalAlignment;


            return cellStyle;
        }

        /// <summary>
        /// Creates the data cell font.
        /// </summary>
        public virtual IFont CreateDataCellFont(IWorkbook workbook, DataFontAttribute? fontAttr)
        {
            var font = workbook.CreateFont();

            fontAttr = fontAttr ?? new DataFontAttribute();

            font.FontName = fontAttr.FontName;
            font.IsItalic = fontAttr.IsItalic;
            font.IsStrikeout = fontAttr.IsStrikeout;
            font.IsBold = fontAttr.IsBold;

            if (fontAttr.Color > -1) font.Color = fontAttr.Color;
            if (fontAttr.FontHeight > -1) font.FontHeight = fontAttr.FontHeight;
            if (fontAttr.TypeOffset > -1) font.TypeOffset = (FontSuperScript)fontAttr.TypeOffset;
            if (fontAttr.Underline > -1) font.Underline = (FontUnderlineType)fontAttr.Underline;
            if (fontAttr.FontHeightInPoints > -1) font.FontHeightInPoints = fontAttr.FontHeightInPoints;
            if (fontAttr.Charset > -1) font.Charset = fontAttr.Charset;

            return font;
        }
    }
}
