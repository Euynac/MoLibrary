using System.Reflection;

namespace Monica.Office.Excel.Models
{
    /// <summary>
    /// Exported Excel header information.
    /// </summary>
    public class ExcelExportHeaderInfo
    {
        /// <summary>
        /// Associated property.
        /// </summary>
        public required PropertyInfo PropertyInfo { get; set; }

        /// <summary>
        /// Displayed header name.
        /// </summary>
        public required string HeaderName { get; set; }

        /// <summary>
        /// Dynamic export settings.
        /// </summary>
        public ExcelHeaderRequest? Option { get; set; }
    }
    /// <summary>
    /// Exported Excel header information together with its data settings.
    /// </summary>
    public record ExcelExportHeaderInfoBundle<TCellStyle, THeaderStyleAttr, THeaderFontAttr, TDataStyleAttr, TDataFontAttr>(ExcelExportHeaderInfo Header)
        where THeaderStyleAttr : Attribute
        where THeaderFontAttr : Attribute
        where TDataStyleAttr : Attribute
        where TDataFontAttr : Attribute
    {
        public ExcelCellStyleOutput<TCellStyle, THeaderStyleAttr, THeaderFontAttr>? HeaderStyle { get; init; } 
        public ExcelCellStyleOutput<TCellStyle, TDataStyleAttr, TDataFontAttr>? DataStyle { get; init; } 
    }
}
