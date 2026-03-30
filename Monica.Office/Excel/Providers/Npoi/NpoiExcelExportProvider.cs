using Monica.Office.Excel.Models;
using Monica.Office.Excel.Services;
using Monica.StateStore.ProgressBar;

namespace Monica.Office.Excel.Providers.Npoi
{   
    /// <summary>
    /// NPOI Excel export provider
    /// </summary>
    /// <remarks>
    /// Initializes a new instance.
    /// </remarks>
    internal class NpoiExcelExportProvider(INpoiCellStyleSupport npoiCellStyleSupport, INpoiWorkbookSupport npoiWorkbookSupport) : ExcelExportService
    {
        protected override byte[] ImplementExport<TExportDto>(IReadOnlyList<TExportDto> data,
            ExcelHeaderRequest[] requests,
            Action<ExcelExportOptions>? optionAction, ProgressBar? progressBar = null)
        {
            var export = new NpoiExcelExportBase(npoiCellStyleSupport, npoiWorkbookSupport);

            return export.Export(data, optionAction, requests, progressBar);
        }
    }
}
