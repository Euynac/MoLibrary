using MoLibrary.Office.Excel.Models;

namespace MoLibrary.Office.Excel.Npoi.Export
{
    /// <summary>
    /// Npoi excel 导出服务
    /// </summary>
    /// <remarks>
    /// 构造
    /// </remarks>
    public class NpoiExcelExportProvider(INpoiCellStyleHandle npoiCellStyleHandle, INpoiExcelHandle npoiExcelHandle) : ExcelExportManager
    {
        protected override byte[] ImplementExport<TExportDto>(List<TExportDto> data, Action<ExcelExportOptions> optionAction, string[] onlyExportHeaderName)
        {
            var export = new NpoiExcelExportBase(npoiCellStyleHandle, npoiExcelHandle);

            return export.Export(data, optionAction, onlyExportHeaderName);
        }
    }
}
