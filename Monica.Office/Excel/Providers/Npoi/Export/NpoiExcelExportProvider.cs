using Monica.Office.Excel.Models;
using Monica.Office.Excel.Services;
using Monica.StateStore.ProgressBar;

namespace Monica.Office.Excel.Providers.Npoi.Export
{   
    /// <summary>
    /// NPOI Excel export provider
    /// </summary>
    /// <remarks>
    /// Initializes a new instance.
    /// </remarks>
    internal class NpoiExcelExportProvider(INpoiCellStyleHandle npoiCellStyleHandle, INpoiExcelHandle npoiExcelHandle) : ExcelExportService
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
