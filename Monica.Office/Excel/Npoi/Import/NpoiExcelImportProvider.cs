using Monica.Office.Excel.Models;

namespace Monica.Office.Excel.Npoi.Import
{
    /// <summary>
    /// NPOI Excel import provider
    /// </summary>
    /// <remarks>
    /// Initializes a new instance.
    /// </remarks>
    public class NpoiExcelImportProvider(INpoiExcelHandle npoiExcelHandle) : ExcelImportManager
    {
        protected override List<ExcelSheetDataOutput<TImportDto>> ImplementImport<TImportDto>(Stream fileStream, Action<ExcelImportOptions>? optionAction)
        {
            var import = new NpoiExcelImportBase(npoiExcelHandle);

            return import.ProcessExcelFile<TImportDto>(fileStream, optionAction);
        }
    }
}
