using MoLibrary.Office.Excel.Models;
using MoLibrary.StateStore.ProgressBar;

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
        protected override byte[] ImplementExport<TExportDto>(IReadOnlyList<TExportDto> data,
            ExcelHeaderRequest[] requests,
            Action<ExcelExportOptions>? optionAction, ProgressBar? progressBar = null)
        {
            var export = new NpoiExcelExportBase(npoiCellStyleHandle, npoiExcelHandle);

            return export.Export(data, optionAction, requests, progressBar);
        }
    }
}
