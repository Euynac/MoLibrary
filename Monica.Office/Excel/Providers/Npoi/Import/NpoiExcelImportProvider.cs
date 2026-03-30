using Monica.Office.Excel.Models;
using Monica.Office.Excel.Services;

namespace Monica.Office.Excel.Providers.Npoi.Import
{
    /// <summary>
    /// NPOI Excel import provider
    /// </summary>
    /// <remarks>
    /// Initializes a new instance.
    /// </remarks>
    internal class NpoiExcelImportProvider(INpoiExcelHandle npoiExcelHandle) : ExcelImportService
    {
        protected override List<ExcelSheetImportResult<TImportDto>> ImplementImport<TImportDto>(Stream fileStream, Action<ExcelImportOptions>? optionAction)
        {
            var import = new NpoiExcelImportBase(npoiExcelHandle);

            return import.ProcessExcelFile<TImportDto>(fileStream, optionAction);
        }
    }
}
