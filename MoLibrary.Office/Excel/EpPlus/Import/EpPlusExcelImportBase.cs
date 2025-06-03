using System.Reflection;
using MoLibrary.Office.Excel.Models;
using OfficeOpenXml;

namespace MoLibrary.Office.Excel.EpPlus.Import
{
    /// <summary>
    /// EpPlus Excel 导入实现（版本号5.0.0之前的为免费版）
    /// </summary>
    /// <remarks>
    /// 构造
    /// </remarks>
    public class EpPlusExcelImportBase(IEpPlusExcelHandle epPlusExcelHandle) : ExcelImportBase<ExcelWorkbook, ExcelWorksheet, ExcelRow, ExcelRange>
    {
        protected override ExcelWorkbook GetWorkbook(Stream fileStream)
        {
            var excelPackage = new ExcelPackage(fileStream);
            return excelPackage.Workbook;
        }

        protected override int GetWorksheetNumber(ExcelWorkbook workbook)
        {
            return workbook.Worksheets.Count;
        }

        protected override ExcelWorksheet GetWorksheet(ExcelWorkbook workbook, int sheetIndex)
        {
            return workbook.Worksheets[sheetIndex];
        }

        protected override string GetWorksheetName(ExcelWorkbook workbook, ExcelWorksheet worksheet)
        {
            return worksheet.Name;
        }

        protected override ExcelRow GetHeaderRow(ExcelWorkbook workbook, ExcelWorksheet worksheet, ExcelImportOptions options)
        {
            return worksheet.Row(options.HeaderRowIndex);
        }

        protected override List<ExcelHeaderCell> GetHeaderCells(ExcelWorkbook workbook, ExcelWorksheet worksheet, ExcelRow headerRow)
        {
            var headerCells = new List<ExcelHeaderCell>();

            for (var i = 1; i <= worksheet.Dimension.End.Column; i++)
            {
                var name = epPlusExcelHandle.GetMergedCellValue(worksheet, headerRow.Row, i)?.ToString();

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                headerCells.Add(new ExcelHeaderCell(name, headerRow.Row, i));
            }

            return headerCells;
        }

        protected override ExcelDataRowRangeIndex GetDataRowStartAndEndRowIndex(ExcelWorkbook workbook, ExcelWorksheet worksheet, ExcelImportOptions options)
        {
            var startRowIndex = options.DataRowStartIndex - 1;
            var endRowIndex = worksheet.Dimension.End.Row - 1;
            if (options.DataRowEndIndex != null)
            {
                var end = (int)options.DataRowEndIndex - 1;
                endRowIndex = end > endRowIndex ? endRowIndex : end;
            }
            return new ExcelDataRowRangeIndex(startRowIndex, endRowIndex);
        }

        protected override ExcelRow GetDataRow(ExcelWorkbook workbook, ExcelWorksheet worksheet, int rowIndex)
        {
            return worksheet.Row(rowIndex + 1);
        }


        protected override object ConvertCellValue(ExcelWorkbook workbook, ExcelWorksheet worksheet, ExcelRow dataRow, int columnIndex, PropertyInfo property)
        {
            return epPlusExcelHandle.ConverterCellValue(worksheet, dataRow.Row, columnIndex, property.PropertyType);
        }

        protected override string GetCellAddress(ExcelWorkbook workbook, ExcelWorksheet worksheet, ExcelRow dataRow, int columnIndex)
        {
            return epPlusExcelHandle.GetCellAddress(dataRow.Row, columnIndex);
        }
    }
}
