using Monica.Office.Excel.Models;
using Monica.Office.Excel.Services;
using Monica.StateStore.ProgressBar;

namespace Monica.Office.Excel.Providers.EpPlus.Export
{
    /// <summary>
    /// EpPlus Excel export provider
    /// </summary>
    /// <remarks>
    /// Initializes a new instance.
    /// </remarks>
    internal class EpPlusExcelExportProvider(IEpPlusCellStyleHandle epPlusCellStyleHandle, IEpPlusExcelHandle epPlusExcelHandle) : ExcelExportService
    {
        protected override byte[] ImplementExport<TExportDto>(IReadOnlyList<TExportDto> data,
            ExcelHeaderRequest[] requests,
            Action<ExcelExportOptions>? optionAction, ProgressBar? progressBar = null)
        {
            var export = new EpPlusExcelExportBase(epPlusCellStyleHandle, epPlusExcelHandle);

            return export.Export(data, optionAction, requests, progressBar);
        }
    }
}
