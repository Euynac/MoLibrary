using Monica.Office.Excel.Models;
using Monica.Office.Excel.Services;
using Monica.StateStore.ProgressBar;

namespace Monica.Office.Excel.Providers.EpPlus
{
    /// <summary>
    /// EpPlus Excel export provider
    /// </summary>
    /// <remarks>
    /// Initializes a new instance.
    /// </remarks>
    internal class EpPlusExcelExportProvider(IEpPlusCellStyleSupport epPlusCellStyleSupport, IEpPlusWorkbookSupport epPlusWorkbookSupport) : ExcelExportService
    {
        protected override byte[] ImplementExport<TExportDto>(IReadOnlyList<TExportDto> data,
            ExcelHeaderRequest[] requests,
            Action<ExcelExportOptions>? optionAction, ProgressBar? progressBar = null)
        {
            var export = new EpPlusExcelExportBase(epPlusCellStyleSupport, epPlusWorkbookSupport);

            return export.Export(data, optionAction, requests, progressBar);
        }
    }
}
