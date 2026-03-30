using Monica.Office.Excel.Models;
using Monica.Office.Excel.Services;

namespace Monica.Office.Excel.Providers.EpPlus.Import
{
    /// <summary>
    /// EpPlus Excel import provider
    /// </summary>
    /// <remarks>
    /// Initializes a new instance.
    /// </remarks>
    internal class EpPlusExcelImportProvider(IEpPlusExcelHandle epPlusExcelHandle) : ExcelImportService
    {
        protected override List<ExcelSheetImportResult<TImportDto>> ImplementImport<TImportDto>(Stream fileStream, Action<ExcelImportOptions>? optionAction)
        {
            var import = new EpPlusExcelImportBase(epPlusExcelHandle);

            return import.ProcessExcelFile<TImportDto>(fileStream, optionAction);
        }
    }
}
