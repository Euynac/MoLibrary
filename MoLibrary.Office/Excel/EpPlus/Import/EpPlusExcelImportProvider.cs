using MoLibrary.Office.Excel.Models;

namespace MoLibrary.Office.Excel.EpPlus.Import
{
    /// <summary>
    /// EpPlus excel 导入服务
    /// </summary>
    /// <remarks>
    /// 构造
    /// </remarks>
    public class EpPlusExcelImportProvider(IEpPlusExcelHandle epPlusExcelHandle) : ExcelImportManager
    {
        protected override List<ExcelSheetDataOutput<TImportDto>> ImplementImport<TImportDto>(Stream fileStream, Action<ExcelImportOptions> optionAction)
        {
            var import = new EpPlusExcelImportBase(epPlusExcelHandle);

            return import.ProcessExcelFile<TImportDto>(fileStream, optionAction);
        }
    }
}
