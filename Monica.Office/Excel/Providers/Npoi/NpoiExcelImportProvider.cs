using Monica.Office.Excel.Models;
using Monica.Office.Excel.Services;

namespace Monica.Office.Excel.Providers.Npoi
{
    /// <summary>
    /// NPOI Excel import provider
    /// </summary>
    /// <remarks>
    /// Initializes a new instance.
    /// </remarks>
    internal class NpoiExcelImportProvider(INpoiWorkbookSupport npoiWorkbookSupport) : ExcelImportService
    {
        protected override List<ExcelSheetImportResult<TImportDto>> ImplementImport<TImportDto>(Stream fileStream, Action<ExcelImportOptions>? optionAction)
        {
            var import = new NpoiExcelImportBase(npoiWorkbookSupport);

            return import.ProcessExcelFile<TImportDto>(fileStream, optionAction);
        }
    }
}
