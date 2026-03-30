using Monica.Office.Excel.Models;
using Monica.Office.Excel.Services;

namespace Monica.Office.Excel.Providers.EpPlus
{
    /// <summary>
    /// EpPlus Excel import provider
    /// </summary>
    /// <remarks>
    /// Initializes a new instance.
    /// </remarks>
    internal class EpPlusExcelImportProvider(IEpPlusWorkbookSupport epPlusWorkbookSupport) : ExcelImportService
    {
        protected override List<ExcelSheetImportResult<TImportDto>> ImplementImport<TImportDto>(Stream fileStream, Action<ExcelImportOptions>? optionAction)
        {
            var import = new EpPlusExcelImportBase(epPlusWorkbookSupport);

            return import.ProcessExcelFile<TImportDto>(fileStream, optionAction);
        }
    }
}
