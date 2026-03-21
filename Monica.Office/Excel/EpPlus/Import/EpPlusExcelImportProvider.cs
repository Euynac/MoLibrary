using Monica.Office.Excel.Models;

namespace Monica.Office.Excel.EpPlus.Import
{
    /// <summary>
    /// EpPlus Excel import provider
    /// </summary>
    /// <remarks>
    /// Initializes a new instance.
    /// </remarks>
    public class EpPlusExcelImportProvider(IEpPlusExcelHandle epPlusExcelHandle) : ExcelImportManager
    {
        protected override List<ExcelSheetDataOutput<TImportDto>> ImplementImport<TImportDto>(Stream fileStream, Action<ExcelImportOptions>? optionAction)
        {
            var import = new EpPlusExcelImportBase(epPlusExcelHandle);

            return import.ProcessExcelFile<TImportDto>(fileStream, optionAction);
        }
    }
}
