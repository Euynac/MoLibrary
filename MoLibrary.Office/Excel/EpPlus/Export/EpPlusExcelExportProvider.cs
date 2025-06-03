using MoLibrary.Office.Excel.Models;

namespace MoLibrary.Office.Excel.EpPlus.Export
{
    /// <summary>
    /// EpPlus excel 导出服务
    /// </summary>
    /// <remarks>
    /// 构造
    /// </remarks>
    public class EpPlusExcelExportProvider(IEpPlusCellStyleHandle epPlusCellStyleHandle, IEpPlusExcelHandle epPlusExcelHandle) : ExcelExportManager
    {
        protected override byte[] ImplementExport<TExportDto>(List<TExportDto> data, Action<ExcelExportOptions> optionAction, string[] onlyExportHeaderName)
        {
            var export = new EpPlusExcelExportBase(epPlusCellStyleHandle, epPlusExcelHandle);

            return export.Export(data, optionAction, onlyExportHeaderName);
        }
    }
}
