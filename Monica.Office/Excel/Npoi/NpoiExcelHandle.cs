using System.ComponentModel;
using System.Globalization;
using NPOI.HSSF.UserModel;
using NPOI.HSSF.Util;
using NPOI.SS.Formula.Constant;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace Monica.Office.Excel.Npoi
{
    /// <summary>
    /// NPOI workbook handler
    /// </summary>
    public class NpoiExcelHandle : INpoiExcelHandle
    {
        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public NpoiExcelHandle()
        {
        }

        /// <summary>
        /// Gets an <see cref="IWorkbook"/>.
        /// </summary>
        /// <param name="physicalPath">The physical file path.</param>
        /// <returns></returns>
        public virtual IWorkbook GetWorkbook(string physicalPath)
        {
            ExcelHelper.ValidationExcel(physicalPath);

            using var stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read);
            return GetWorkbook(stream);
        }

        /// <summary>
        /// Gets an <see cref="IWorkbook"/>.
        /// </summary>
        /// <param name="fileStream">The file stream.</param>
        /// <returns></returns>
        public virtual IWorkbook GetWorkbook(Stream fileStream)
        {
            try
            {
                var workbook = WorkbookFactory.Create(fileStream);

                //try
                //{
                //    // XSSFWorkbook: Excel >= 2007, .xlsx, 1,048,576 rows and 16,384 columns
                //    // SXSSFWorkbook: Excel >= 2007, .xlsx, trades disk space for memory and is suitable for large datasets
                //    workbook = new XSSFWorkbook(fileStream);
                //}
                //catch
                //{
                //    fileStream.Position = 0;
                //    // Excel <= 2003, .xls, 65,535 rows and 256 columns
                //    workbook = new HSSFWorkbook(fileStream);

                //}

                return workbook;
            }
            catch (Exception e)
            {
                throw new Exception($"获取 Workbook 出错：{e.Message}", e);
            }
        }

        /// <summary>
        /// Gets merged-cell metadata for a cell.
        /// </summary>
        /// <param name="sheet">The worksheet.</param>
        /// <param name="cell">The cell.</param>
        /// <returns>MergedInfo</returns>
        public virtual ExcelCellMergedInfo GetCellMergedInfo(ISheet sheet, ICell? cell)
        {
            if (cell?.IsMergedCell == true)
            {
                // Get the number of merged regions in the worksheet.
                for (var i = 0; i < sheet.NumMergedRegions; i++)
                {
                    // Get the merged region.
                    var ca = sheet.GetMergedRegion(i);
                    // Check whether the cell falls within the merged region bounds.
                    if (cell.ColumnIndex <= ca.LastColumn && cell.ColumnIndex >= ca.FirstColumn)
                    {
                        if (cell.RowIndex <= ca.LastRow && cell.RowIndex >= ca.FirstRow)
                        {
                            return new ExcelCellMergedInfo(cell, ca);
                        }
                    }
                }
            }

            return new ExcelCellMergedInfo(cell);
        }

        /// <summary>
        /// Gets the cell value.
        /// </summary>
        /// <param name="cell">The cell.</param>
        /// <returns></returns>
        public virtual object? GetCellValue(ICell? cell)
        {
            if (cell == null)
            {
                return null;
            }

            try
            {
                object? value = null;
                switch (cell.CellType)
                {
                    case CellType.Blank: // Blank value 3
                        value = null;
                        break;
                    case CellType.Unknown: // Unknown -1
                    case CellType.String: // String 1
                        value = cell.StringCellValue;
                        break;
                    case CellType.Boolean: // Boolean 4
                        value = cell.BooleanCellValue;
                        break;
                    case CellType.Error: // Error 5
                        try
                        {
                            value = ErrorConstant.ValueOf(cell.ErrorCellValue).Text;
                        }
                        catch
                        {
                            value = cell.ErrorCellValue;
                        }
                        break;
                    case CellType.Numeric: // Numeric 0

                        if (DateUtil.IsCellDateFormatted(cell) || DateUtil.IsCellInternalDateFormatted(cell))
                        {
                            value = DateTime.FromOADate(cell.NumericCellValue);
                        }
                        else
                        {
                            value = cell.NumericCellValue;
                        }
                        break;
                    case CellType.Formula: // Formula 2
                        try
                        {
                            var eva = new HSSFFormulaEvaluator(cell.Sheet.Workbook);
                            value = GetCellValue(eva.Evaluate(cell), cell);
                        }
                        catch
                        {
                            var e = new XSSFFormulaEvaluator(cell.Sheet.Workbook);
                            value = GetCellValue(e.Evaluate(cell), cell);
                        }
                        break;
                    default:
                        value = cell.StringCellValue;
                        break;
                }

                return value;
            }
            catch (Exception e)
            {
                throw new Exception($"获取单元格值出错[{new CellReference(cell).FormatAsString()}]： {e.Message}", e);
            }
        }
        /// <summary>
        /// Gets the value of a formula cell.
        /// </summary>
        /// <param name="formulaValue"></param>
        /// <param name="cell"></param>
        /// <returns></returns>
        public virtual object? GetCellValue(CellValue? formulaValue, ICell? cell)
        {
            if (formulaValue == null || cell == null)
            {
                return formulaValue;
            }
            object? value = null;
            switch (formulaValue.CellType)
            {
                case CellType.Blank:
                    value = null;
                    break;
                case CellType.Unknown:
                case CellType.String:
                    value = formulaValue.StringValue;
                    break;
                case CellType.Boolean:
                    value = formulaValue.BooleanValue.ToString(CultureInfo.CurrentCulture);
                    break;
                case CellType.Error:
                    try
                    {
                        value = ErrorConstant.ValueOf(cell.ErrorCellValue).Text;
                    }
                    catch
                    {
                        value = cell.ErrorCellValue.ToString();
                    }
                    break;
                case CellType.Numeric:
                    value = formulaValue.NumberValue.ToString(CultureInfo.CurrentCulture);
                    break;
                case CellType.Formula:
                    try
                    {
                        var eva = new HSSFFormulaEvaluator(cell.Sheet.Workbook);
                        value = GetCellValue(eva.Evaluate(cell), cell);
                    }
                    catch
                    {
                        var e = new XSSFFormulaEvaluator(cell.Sheet.Workbook);
                        value = GetCellValue(e.Evaluate(cell), cell);
                    }
                    break;
                default:
                    value = formulaValue.StringValue;
                    break;
            }

            return value;
        }

        /// <summary>
        /// Gets the value from the merged cell range that contains the cell.
        /// </summary>
        /// <param name="sheet">The worksheet.</param>
        /// <param name="cell">The cell.</param>
        /// <returns>Returns the first value in the merged range for a merged cell; otherwise returns the current cell value.</returns>
        public virtual object? GetMergedCellValue(ISheet sheet, ICell cell)
        {
            var info = GetCellMergedInfo(sheet, cell);
            if (info.IsMergedRegion && info.CellRangeAddress is { } cellRangeAddress)
            {
                var fRow = sheet.GetRow(cellRangeAddress.FirstRow);
                var fCell = fRow?.GetCell(cellRangeAddress.FirstColumn);
                return GetCellValue(fCell);
            }
            return GetCellValue(cell);
        }

        /// <summary>
        /// Gets the default format for a cell value.
        /// </summary>
        /// <param name="workbook"></param>
        /// <param name="cellValue"></param>
        /// <returns></returns>
        public virtual short GetDefaultFormat(IWorkbook workbook, CellValueDefaultFormatEnum cellValue = CellValueDefaultFormatEnum.文本)
        {
            var format = workbook.CreateDataFormat();
            switch (cellValue)
            {
                case CellValueDefaultFormatEnum.日期:
                    return format.GetFormat("yyyy-MM-dd HH:mm:ss");
                case CellValueDefaultFormatEnum.时间:
                    return format.GetFormat("HH:mm:ss");
                case CellValueDefaultFormatEnum.数字:
                    return format.GetFormat("0.00"); // Each trailing zero controls the number of decimal places.
                case CellValueDefaultFormatEnum.金额:
                    return format.GetFormat("￥#,##0.00_ ");
                case CellValueDefaultFormatEnum.百分比:
                    return format.GetFormat("0.00%");
                case CellValueDefaultFormatEnum.中文大写:
                    return format.GetFormat("[DbNum2][$-804]0");
                case CellValueDefaultFormatEnum.科学计数法:
                    return format.GetFormat("0.00E+00");
                case CellValueDefaultFormatEnum.文本:
                    return HSSFDataFormat.GetBuiltinFormat("@");
                default:
                    return HSSFDataFormat.GetBuiltinFormat("@");
            }
        }

        /// <summary>
        /// Converts a cell value.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="columnIndex">The current column index, zero-based.</param>
        /// <param name="valueType">The target value type, for example <c>PropertyInfo.PropertyType</c>, <c>typeof(int?)</c>, <c>typeof(bool)</c>, or <c>typeof(string)</c>.</param>
        /// <returns></returns>
        public virtual object? ConverterCellValue(IRow? row, int columnIndex, Type valueType)
        {
            var cell = row?.GetCell(columnIndex);
            if (cell == null)
            {
                return null;
            }

            var cellValue = GetMergedCellValue(cell.Sheet, cell);
            return cellValue.ConvertExcelCellValue(valueType);
        }

        /// <summary>
        /// Gets the default cell style.
        /// <para>An <c>.xls</c> workbook can define at most 4000 styles, so call this outside loops.</para>
        /// </summary>
        /// <param name="workbook">Workbook</param>
        /// <param name="style">CellStyleEnum</param>
        /// <returns></returns>
        public virtual ICellStyle GetDefaultCellStyle(IWorkbook workbook, ExcelCellStyleEnum style = ExcelCellStyleEnum.默认)
        {
            var cellStyle = workbook.CreateCellStyle();

            //// Border
            //cellStyle.BorderTop = BorderStyle.Dotted;
            //cellStyle.BorderBottom = BorderStyle.Dotted;
            //cellStyle.BorderLeft = BorderStyle.Hair;
            //cellStyle.BorderRight = BorderStyle.Hair;

            //// Border colors
            //cellStyle.BottomBorderColor = HSSFColor.Blue.Index;
            //cellStyle.TopBorderColor = HSSFColor.Blue.Index;

            //// Background
            //cellStyle.FillBackgroundColor = HSSFColor.Blue.Index;
            //cellStyle.FillForegroundColor = HSSFColor.Blue.Index;
            //cellStyle.FillForegroundColor = HSSFColor.White.Index;
            //cellStyle.FillPattern = FillPattern.NoFill;
            //cellStyle.FillBackgroundColor = HSSFColor.Blue.Index;

            // Horizontal alignment
            cellStyle.Alignment = HorizontalAlignment.Center;
            // Vertical alignment
            cellStyle.VerticalAlignment = VerticalAlignment.Center;
            // Wrap text
            cellStyle.WrapText = true;
            // Indentation
            cellStyle.Indention = 0;

            // Font
            var font = workbook.CreateFont();
            font.FontHeightInPoints = 10;// Set font size.
            font.FontName = "微软雅黑";// Font name
            font.Color = HSSFColor.Black.Index;// Font color

            switch (style)
            {
                case ExcelCellStyleEnum.网址:
                    font.Color = HSSFColor.Blue.Index;
                    font.IsItalic = true;
                    font.Underline = FontUnderlineType.Single;
                    break;
                case ExcelCellStyleEnum.主标题:
                    font.IsBold = true;
                    font.FontHeightInPoints = 22;
                    break;
                case ExcelCellStyleEnum.表头:
                    font.IsBold = true;
                    font.FontHeightInPoints = 12;
                    break;
                default:
                    break;
            }
            cellStyle.SetFont(font);

            return cellStyle;
        }

        /// <summary>
        /// Sets the width for a single column. Call this after creating the column. Auto-fit requires existing cell data.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="columnIndex">The column index, zero-based.</param>
        /// <param name="columnSize">The column width in characters, from 0 to 255.</param>
        /// <param name="columnAutoSize">Whether to auto-fit the column.</param>
        public virtual void SetColumnWidth(ISheet sheet, int columnIndex, int columnSize, bool columnAutoSize)
        {
            if (columnSize > 0)
            {
                // The column must already exist before setting the width.
                sheet.SetColumnWidth(columnIndex, (columnSize > 255 ? 255 : columnSize) * 256);
            }
            else if (columnAutoSize)
            {
                // Auto-fit requires existing cell data in the column.
                sheet.AutoSizeColumn(columnIndex);
            }
        }

        /// <summary>
        /// Sets the default width for all columns. Call this after creating the worksheet and before creating columns.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="columnSize">The column width in characters, from 0 to 255.</param>
        public virtual void SetColumnWidth(ISheet sheet, int columnSize)
        {
            if (columnSize > 0)
            {
                // Set this after creating the worksheet and before creating columns.
                sheet.DefaultColumnWidth = columnSize > 255 ? 255 : columnSize;// No need to multiply by 256 here.
            }
        }

        /// <summary>
        /// Sets the height for a single row. Call this after creating the row.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="row">The row.</param>
        /// <param name="rowHeight">The row height in points, from 0 to 409.</param>
        public virtual void SetRowHeight(ISheet sheet, IRow row, short rowHeight)
        {
            if (rowHeight > 0)
            {
                // The row must already exist before setting the height.
                row.Height = (short)((rowHeight > 409 ? 409 : rowHeight) * 20.0);
            }
        }

        /// <summary>
        /// Sets the default height for all rows. Call this immediately after creating the worksheet, before creating rows.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="rowHeight">The row height in points, from 0 to 409.</param>
        public virtual void SetRowHeight(ISheet sheet, short rowHeight)
        {
            if (rowHeight > 0)
            {
                // Set this immediately after creating the worksheet and before creating rows.
                sheet.DefaultRowHeight = (short)((rowHeight > 409 ? 409 : rowHeight) * 20.0);
            }
        }

        /// <summary>
        /// Writes the workbook to a stream and returns the bytes.
        /// </summary>
        /// <param name="workbook"></param>
        /// <returns></returns>
        public virtual byte[] GetAsByteArray(IWorkbook workbook)
        {
            var ms = new MemoryStream();
            workbook.Write(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Merges a cell range.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="firstRow">The start row index, zero-based.</param>
        /// <param name="lastRow">The end row index, zero-based.</param>
        /// <param name="firstCol">The start column index, zero-based.</param>
        /// <param name="lastCol">The end column index, zero-based.</param>
        public virtual void MergedRegion(ISheet sheet, int firstRow, int lastRow, int firstCol, int lastCol)
        {
            // Merge the range.
            sheet.AddMergedRegion(new CellRangeAddress(firstRow, lastRow, firstCol, lastCol));
        }

        /// <summary>
        /// Gets the address string for a cell, for example <c>A1</c>.
        /// </summary>
        /// <param name="rowIndex">The row index, zero-based.</param>
        /// <param name="columnIndex">The column index, zero-based.</param>
        public virtual string GetCellAddress(int rowIndex, int columnIndex)
        {
            return new CellReference(rowIndex, columnIndex).FormatAsString();
        }

        /// <summary>
        /// Gets the address string for a cell, for example <c>A1</c>.
        /// </summary>
        /// <param name="cell">The cell.</param>
        public virtual string GetCellAddress(ICell cell)
        {
            return new CellReference(cell).FormatAsString();
        }
    }



    /// <summary>
    /// Metadata for a merged cell range
    /// </summary>
    public class ExcelCellMergedInfo
    {
        /// <summary>
        /// Gets a value indicating whether the cell belongs to a merged range.
        /// </summary>
        public bool IsMergedRegion { get; }
        /// <summary>
        /// Gets the cell range address.
        /// </summary>
        public CellRangeAddress? CellRangeAddress { get; }
        /// <summary>
        /// Gets the cell.
        /// </summary>
        public ICell? ICell { get; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelCellMergedInfo(ICell? cell)
        {
            ICell = cell;
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cell"></param>
        /// <param name="cellRangeAddress"></param>
        public ExcelCellMergedInfo(ICell cell, CellRangeAddress cellRangeAddress)
        {
            ICell = cell;
            IsMergedRegion = true;
            CellRangeAddress = cellRangeAddress;
        }
    }

    /// <summary>
    /// Cell data format types
    /// </summary>
    public enum CellValueDefaultFormatEnum
    {
        文本,
        数字,
        日期,
        时间,
        金额,
        百分比,
        中文大写,
        科学计数法,
    }

    /// <summary>
    /// Cell style types
    /// </summary>
    public enum ExcelCellStyleEnum
    {
        默认,
        主标题,
        表头,
        网址,
    }

}
