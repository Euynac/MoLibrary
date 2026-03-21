using Monica.Office.Excel.Models;
using Monica.StateStore.ProgressBar;

namespace Monica.Office.Excel.EpPlus.Export
{
    /// <summary>
    /// EpPlus Excel export provider
    /// </summary>
    /// <remarks>
    /// Initializes a new instance.
    /// </remarks>
    public class EpPlusExcelExportProvider(IEpPlusCellStyleHandle epPlusCellStyleHandle, IEpPlusExcelHandle epPlusExcelHandle) : ExcelExportManager
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
