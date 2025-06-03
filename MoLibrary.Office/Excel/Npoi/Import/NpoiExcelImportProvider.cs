using MoLibrary.Office.Excel.Models;

namespace MoLibrary.Office.Excel.Npoi.Import
{
    /// <summary>
    /// Npoi excel 导入服务
    /// </summary>
    /// <remarks>
    /// 构造
    /// </remarks>
    public class NpoiExcelImportProvider(INpoiExcelHandle npoiExcelHandle) : ExcelImportManager
    {
        protected override List<ExcelSheetDataOutput<TImportDto>> ImplementImport<TImportDto>(Stream fileStream, Action<ExcelImportOptions> optionAction)
        {
            var import = new NpoiExcelImportBase(npoiExcelHandle);

            return import.ProcessExcelFile<TImportDto>(fileStream, optionAction);
        }
    }
}
