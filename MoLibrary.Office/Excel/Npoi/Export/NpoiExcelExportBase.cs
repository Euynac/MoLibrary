using System.Globalization;
using MoLibrary.Office.Excel.Attributes;
using MoLibrary.Office.Excel.Models;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace MoLibrary.Office.Excel.Npoi.Export
{
    /// <summary>
    /// Npoi Excel 导出实现
    /// </summary>
    /// <remarks>
    /// 构造
    /// </remarks>
    public class NpoiExcelExportBase(INpoiCellStyleHandle npoiCellStyleHandle, INpoiExcelHandle npoiExcelHandle) : ExcelExportBase<IWorkbook, ISheet, IRow, ICell, ICellStyle>
    {
        protected override IWorkbook GetWorkbook(ExcelExportOptions options)
        {
            if (options.ExcelType.Equals(ExcelTypeEnum.Xlsx))
            {
                return new XSSFWorkbook();
            }
            else
            {
                return new HSSFWorkbook();
            }
        }

        protected override ISheet CreateSheet(IWorkbook workbook, ExcelExportOptions options)
        {
            return workbook.CreateSheet(options.SheetName);
        }

        protected override ICell CreateCell(IWorkbook workbook, ISheet sheet, int rowIndex, int columnIndex)
        {
            var row = sheet.GetRow(rowIndex);
            if (row == null)
            {
                row = sheet.CreateRow(rowIndex);
            }
            return row.CreateCell(columnIndex);
        }

        protected override void SetCellValue(IWorkbook workbook, ISheet sheet, ICell cell, Type valueType, object? value)
        {
            if (value != null)
            {
                if (valueType.IsDouble())
                {
                    cell.SetCellValue(value.GetTypedCellValue<double>());
                }
                else if (valueType.IsDateTime())
                {
                    var date = value.GetTypedCellValue<DateTime>();
                    if (date == default)
                    {
                        cell.SetCellValue(date.ToString(CultureInfo.CurrentCulture));
                    }
                    else
                    {
                        cell.SetCellValue(date);
                    }
                }
                else if (valueType.IsTimeSpan())
                {
                    cell.SetCellValue(value.GetTypedCellValue<DateTime>().ToString(CultureInfo.CurrentCulture));
                }
                else if (valueType.IsBool())
                {
                    cell.SetCellValue(value.GetTypedCellValue<bool>());
                }
                else
                {
                    cell.SetCellValue(value.ToString());
                }
            }
        }

        protected override ICellStyle CreateHeaderStyleAndFont<TExportDto>(IWorkbook workbook, ISheet worksheet, HeaderStyleAttribute styleAttr, HeaderFontAttribute fontAttr)
        {
            return npoiCellStyleHandle.SetHeaderCellStyleAndFont(workbook, styleAttr, fontAttr);
        }

        protected override ICellStyle CreateDataStyleAndFont<TExportDto>(IWorkbook workbook, ISheet worksheet, DataStyleAttribute styleAttr, DataFontAttribute fontAttr)
        {
            return npoiCellStyleHandle.SetDataCellStyleAndFont(workbook, styleAttr, fontAttr);
        }

        protected override void SetHeaderCellStyleAndFont<TExportDto>(IWorkbook workbook, ISheet worksheet, ICell cell,
            ExcelCellStyleOutput<ICellStyle, HeaderStyleAttribute, HeaderFontAttribute> cellStyleInfo)
        {
            cell.CellStyle = cellStyleInfo.CellStyle;
        }

        protected override void SetDataCellStyleAndFont<TExportDto>(IWorkbook workbook, ISheet worksheet, ICell cell, ExcelCellStyleOutput<ICellStyle, DataStyleAttribute, DataFontAttribute> cellStyleInfo)
        {
            cell.CellStyle = cellStyleInfo.CellStyle;
        }

        protected override void SetColumnWidth(IWorkbook workbook, ISheet sheet, int columnIndex, int columnSize, bool columnAutoSize)
        {
            npoiExcelHandle.SetColumnWidth(sheet, columnIndex, columnSize, columnAutoSize);
        }

        protected override void SetRowHeight(IWorkbook workbook, ISheet worksheet, int rowIndex, short rowHeight)
        {
            npoiExcelHandle.SetRowHeight(worksheet, worksheet.GetRow(rowIndex), rowHeight);
        }

        protected override void SetMergedRegion(IWorkbook workbook, ISheet worksheet, int fromRowIndex, int toRowIndex,
            int fromColumnIndex, int toColumnIndex)
        {
            npoiExcelHandle.MergedRegion(worksheet, fromRowIndex, toRowIndex, fromColumnIndex, toColumnIndex);
        }

        protected override string GetCellAddress(IWorkbook workbook, ISheet worksheet, int rowIndex,int columnIndex)
        {
            return npoiExcelHandle.GetCellAddress(rowIndex, columnIndex);
        }

        protected override void SetCellFormula(IWorkbook workbook, ISheet worksheet, ICell cell, string cellFormula)
        {
            cell.SetCellFormula(cellFormula);
        }

        protected override byte[] GetAsByteArray(IWorkbook workbook, ISheet sheet)
        {
            return npoiExcelHandle.GetAsByteArray(workbook);
        }

    }
}
