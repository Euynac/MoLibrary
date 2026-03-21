using Monica.Office.Excel.Models;
using Monica.StateStore.ProgressBar;

namespace Monica.Office.Excel.Npoi.Export
{   
    /// <summary>
    /// NPOI Excel export provider
    /// </summary>
    /// <remarks>
    /// Initializes a new instance.
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
