using System.Reflection;

namespace MoLibrary.Office.Excel.Models
{
    /// <summary>
    /// excel 导出的表头信息
    /// </summary>
    public class ExcelExportHeaderInfo
    {
        /// <summary>
        /// 对应的属性
        /// </summary>
        public required PropertyInfo PropertyInfo { get; set; }

        /// <summary>
        /// 显示的表头名称
        /// </summary>
        public required string HeaderName { get; set; }

        /// <summary>
        /// 动态的导出设置
        /// </summary>
        public ExcelHeaderRequest? Option { get; set; }
    }
    /// <summary>
    /// excel 导出的表头及其数据设置信息
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
