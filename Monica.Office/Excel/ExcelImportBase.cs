using System.ComponentModel;
using System.Reflection;
using Monica.Office.Excel.Models;

namespace Monica.Office.Excel
{
    /// <summary>
    /// Base class for Excel import
    /// </summary>
    /// <typeparam name="TWorkbook">The workbook type</typeparam>
    /// <typeparam name="TSheet">The worksheet type</typeparam>
    /// <typeparam name="TRow">The row type</typeparam>
    /// <typeparam name="TCell">The cell type</typeparam>
    public abstract class ExcelImportBase<TWorkbook, TSheet, TRow, TCell>
    {
        /// <summary>
        /// Initializes a new instance
        /// </summary>
        protected ExcelImportBase()
        {
        }

        /// <summary>
        /// Processes an Excel file
        /// </summary>
        /// <typeparam name="TImportDto">The DTO type mapped to the header row
        /// <para>Each header cell name maps to the <c>DisplayName</c> attribute in <see cref="System.ComponentModel.DataAnnotations"/>, and field validation can use attributes such as Required, StringLength, Range, and RegularExpression.</para>
        /// </typeparam>
        /// <param name="fileBytes">The Excel file bytes</param>
        /// <param name="optionAction">Configures import options</param>
        /// <returns></returns>
        public List<ExcelSheetDataOutput<TImportDto>> ProcessExcelFile<TImportDto>(
            byte[] fileBytes,
            Action<ExcelImportOptions>? optionAction = null
        ) where TImportDto : class, new()
        {
            try
            {
                using var stream = new MemoryStream(fileBytes);
                return ProcessExcelFile<TImportDto>(stream, optionAction);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message, e);
            }
        }

        /// <summary>
        /// Processes an Excel file
        /// </summary>
        /// <typeparam name="TImportDto">The DTO type mapped to the header row
        /// <para>Each header cell name maps to the <c>DisplayName</c> attribute in <see cref="System.ComponentModel.DataAnnotations"/>, and field validation can use attributes such as Required, StringLength, Range, and RegularExpression.</para>
        /// </typeparam>
        /// <param name="fileStream">The file stream</param>
        /// <param name="optionAction">Configures import options</param>
        /// <returns></returns>
        public List<ExcelSheetDataOutput<TImportDto>> ProcessExcelFile<TImportDto>(
            Stream fileStream,
            Action<ExcelImportOptions>? optionAction = null
        ) where TImportDto : class, new()
        {
            try
            {
                // Apply and validate the options.
                var options = new ExcelImportOptions();
                optionAction?.Invoke(options);
                options.CheckError();

                return ProcessWorkbook<TImportDto>(fileStream, options);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message, e);
            }
        }

        #region Private

        /// <summary>
        /// Processes the workbook
        /// </summary>
        /// <param name="fileStream">The file stream</param>
        /// <param name="options">The import options</param>
        /// <returns></returns>
        private List<ExcelSheetDataOutput<TImportDto>> ProcessWorkbook<TImportDto>(Stream fileStream, ExcelImportOptions options) where TImportDto : class, new()
        {
            var dataList = new List<ExcelSheetDataOutput<TImportDto>>();

            // Workbook
            var workbook = GetWorkbook(fileStream);

            // Worksheet count
            var sheetsCount = GetWorksheetNumber(workbook);

            if (options.SheetIndex > sheetsCount)
            {
                throw new Exception($"工作表 sheet 编号超出：最大只能为 {sheetsCount}");
            }

            // Load worksheet data
            if (options.SheetIndex <= 0)
            {
                // All worksheets
                for (var i = 0; i < sheetsCount; i++)
                {
                    var data = ProcessWorksheet<TImportDto>(workbook, i, options);
                    dataList.Add(data);

                    // Validation mode
                    if (data.InvalidCount > 0)
                    {
                        if (options.ValidateMode.Equals(ExcelValidateModeEnum.StopSheet))
                        {
                            break;
                        }
                        if (options.ValidateMode.Equals(ExcelValidateModeEnum.ThrowSheet))
                        {
                            data.CheckError();
                        }
                    }
                }
            }
            else
            {
                // Single worksheet
                var data = ProcessWorksheet<TImportDto>(workbook, options.SheetIndex - 1, options);
                dataList.Add(data);

                // Validation mode
                if (dataList.Any(a => a.InvalidCount > 0))
                {
                    if (options.ValidateMode.Equals(ExcelValidateModeEnum.ThrowSheet))
                    {
                        dataList.CheckError();
                    }
                }
            }

            // Validation mode
            if (dataList.Any(a => a.InvalidCount > 0))
            {
                if (options.ValidateMode.Equals(ExcelValidateModeEnum.ThrowBook))
                {
                    dataList.CheckError();
                }
            }

            return dataList;
        }

        /// <summary>
        /// Gets the worksheet data
        /// </summary>
        /// <typeparam name="TImportDto"></typeparam>
        /// <param name="workbook">The workbook</param>
        /// <param name="sheetIndex">The worksheet index (zero-based)</param>
        /// <param name="options">The import options</param>
        /// <returns></returns>
        private ExcelSheetDataOutput<TImportDto> ProcessWorksheet<TImportDto>(TWorkbook workbook, int sheetIndex, ExcelImportOptions options) where TImportDto : class, new()
        {
            // Get the worksheet
            var worksheet = GetWorksheet(workbook, sheetIndex);

            // Worksheet name
            var sheetName = GetWorksheetName(workbook, worksheet);

            try
            {
                // Get the header row
                var headerRow = GetHeaderRow(workbook, worksheet, options);

                // Get the header cells
                var headerCells = GetHeaderCells(workbook, worksheet, headerRow);

                // Header cell metadata
                var headerCellInfo = new ExcelHeaderCellInfo(sheetName, sheetIndex, headerCells);

                // Validate the header row
                ValidateHeaderRow<TImportDto>(headerCellInfo);

                // Get the property mapping for the header cells
                var headerCellProperties = GetHeaderCellProperties<TImportDto>(headerCellInfo);

                // Build the worksheet data
                return new ExcelSheetDataOutput<TImportDto>
                {
                    SheetName = sheetName,
                    SheetIndex = sheetIndex + 1,
                    Rows = ProcessWorksheetData<TImportDto>(workbook, worksheet, headerCellInfo, headerCellProperties, options),
                };

            }
            catch (Exception e)
            {
                throw new Exception($"工作表【{sheetName}】存在以下错误：{e.Message}", e);
            }
        }
        /// <summary>
        /// Processes a worksheet
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="headerCellInfo"></param>
        /// <param name="headerCellProperties">The header cell metadata collection</param>
        /// <param name="options">The import options</param>
        /// <returns></returns>
        private List<ExcelImportRowInfo<TImportDto>> ProcessWorksheetData<TImportDto>(TWorkbook workbook, TSheet worksheet, ExcelHeaderCellInfo headerCellInfo, ExcelHeaderCellPropertyInfo headerCellProperties, ExcelImportOptions options) where TImportDto : class, new()
        {
            var rows = new List<ExcelImportRowInfo<TImportDto>>();

            // Get the data row range
            var rowRangeIndex = GetDataRowStartAndEndRowIndex(workbook, worksheet, options);

            for (var i = rowRangeIndex.StartIndex; i <= rowRangeIndex.EndIndex; i++)
            {
                // Get the data row
                var row = GetDataRow(workbook, worksheet, i);

                // Get the row data
                var entity = GetRowData<TImportDto>(workbook, worksheet, headerCellProperties, row);
                if (entity != null)
                {
                    // Validate the row data
                    var errors = ExcelHelper.GetValidationResult(entity) ?? [];

                    var rowInfo = new ExcelImportRowInfo<TImportDto>
                    {
                        SheetName = headerCellInfo.SheetName,
                        SheetIndex = headerCellInfo.SheetIndex,
                        Row = entity,
                        Errors = errors,
                        RowNum = i + 1,
                        IsValid = errors.Count == 0
                    };

                    rows.Add(rowInfo);

                    if (!rowInfo.IsValid)
                    {
                        if (options.ValidateMode.Equals(ExcelValidateModeEnum.StopRow))
                        {
                            break;
                        }

                        if (options.ValidateMode.Equals(ExcelValidateModeEnum.ThrowRow))
                        {
                            rowInfo.CheckError();
                        }
                    }
                }
            }
            return rows;
        }

        /// <summary>
        /// Gets the row data
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="headerCellProperties">The header cell metadata collection</param>
        /// <param name="dataRow">The data row</param>
        /// <returns></returns>
        private TImportDto GetRowData<TImportDto>(TWorkbook workbook, TSheet worksheet, ExcelHeaderCellPropertyInfo headerCellProperties, TRow dataRow) where TImportDto : class, new()
        {
            var data = new TImportDto();

            foreach (var p in headerCellProperties.HeaderCells)
            {
                try
                {
                    var property = p.PropertyInfo;
                    //if (property.SetMethod == null)
                    //{
                    //    continue;
                    //}

                    // Convert the cell value
                    var value = ConvertCellValue(workbook, worksheet, dataRow, p.ColumnIndex, property);
                    if (value == null)
                    {
                        var defaultValue = property.GetCustomAttribute<DefaultValueAttribute>();
                        if (defaultValue != null)
                        {
                            value = defaultValue.Value;
                        }
                    }

                    property.SetValue(data, value, null);
                }
                catch (Exception e)
                {
                    var cellAddress = GetCellAddress(workbook, worksheet, dataRow, p.ColumnIndex);

                    throw new Exception($"表头【{ p.Name }】在【{cellAddress}】的数据转换失败：{e.Message}", e);
                }
            }
            return data;
        }

        /// <summary>
        /// Validates the header row
        /// </summary>
        /// <param name="headerCellInfo">The header cell metadata collection</param>
        private void ValidateHeaderRow<TImportDto>(ExcelHeaderCellInfo headerCellInfo) where TImportDto : class, new()
        {
            if (headerCellInfo.HeaderCells?.Any() != true)
            {
                throw new Exception($"表头行不能为空");
            }

            // Property names
            var propertyNames = ExcelHelper.GetDisplayNameListFromProperty<TImportDto>();

            if (!propertyNames.Any())
            {
                throw new Exception($"类 {typeof(TImportDto).Name} 对应的表头不能为空");
            }
            var propertyDuplicate = propertyNames.GroupBy(a => a).Where(a => a.Count() > 1).Select(a => a.Key).ToList();
            if (propertyDuplicate.Any())
            {
                throw new Exception($"类 {typeof(TImportDto).Name} 中 Display Name 重复（或与属性名称重复）：{string.Join(",", propertyDuplicate)}");
            }

            // Excel header names
            var headerNames = headerCellInfo.HeaderCells.Select(a => a.Name).ToList();
            var headerDuplicate = headerNames.GroupBy(a => a).Where(a => a.Count() > 1).Select(a => a.Key).ToList();
            if (headerDuplicate.Any())
            {
                throw new Exception($"表头名称重复：{string.Join(",", headerDuplicate)}");
            }

            var except = propertyNames.Except(headerNames).ToList();
            if (except.Any())
            {
                throw new Exception($"工作表中不存在以下表头名称：{string.Join(",", except)}");
            }
        }

        /// <summary>
        /// Gets the header cell to property mapping
        /// </summary>
        /// <param name="headerCellInfo">The header cell metadata collection</param>
        private ExcelHeaderCellPropertyInfo GetHeaderCellProperties<TImportDto>(ExcelHeaderCellInfo headerCellInfo) where TImportDto : class, new()
        {
            if (headerCellInfo.HeaderCells?.Any() != true)
            {
                throw new Exception($"表头行不能为空");
            }

            var cellProperties = new ExcelHeaderCellPropertyInfo
            {
                SheetName = headerCellInfo.SheetName,
                SheetIndex = headerCellInfo.SheetIndex
            };

            // Properties
            var properties = ExcelHelper.GetProperties<TImportDto>();

            foreach (var p in properties)
            {
                var name = p.GetDisplayNameFromProperty()?.Trim();
                var cell = headerCellInfo.HeaderCells.FirstOrDefault(a => a.Name.Trim() == name);
                if (cell != null)
                {
                    cellProperties.HeaderCells.Add(new ExcelHeaderCellProperty
                    {
                        Name = cell.Name,
                        ColumnIndex = cell.ColumnIndex,
                        RowIndex = cell.RowIndex,
                        PropertyInfo = p
                    });
                }
            }

            return cellProperties;
        }

        #endregion

        #region Abstract methods

        /// <summary>
        /// Gets the workbook [Step 1]
        /// </summary>
        /// <param name="fileStream">The file stream</param>
        /// <returns></returns>
        protected abstract TWorkbook GetWorkbook(Stream fileStream);

        /// <summary>
        /// Gets the worksheet count [Step 2]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <returns></returns>
        protected abstract int GetWorksheetNumber(TWorkbook workbook);

        /// <summary>
        /// Gets the worksheet [Step 3]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="sheetIndex">The worksheet index (zero-based)</param>
        /// <returns></returns>
        protected abstract TSheet GetWorksheet(TWorkbook workbook, int sheetIndex);

        /// <summary>
        /// Gets the worksheet name [Step 4]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <returns></returns>
        protected abstract string GetWorksheetName(TWorkbook workbook, TSheet worksheet);

        /// <summary>
        /// Gets the header row [Step 5]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="options">The import options</param>
        /// <returns></returns>
        protected abstract TRow GetHeaderRow(TWorkbook workbook, TSheet worksheet, ExcelImportOptions options);

        /// <summary>
        /// Gets the header cell metadata collection [Step 6]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="headerRow">The header row</param>
        protected abstract List<ExcelHeaderCell> GetHeaderCells(TWorkbook workbook, TSheet worksheet, TRow headerRow);

        /// <summary>
        /// Gets the start and end indexes of the data rows (zero-based) [Step 7]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="options">The import options</param>
        /// <returns></returns>
        protected abstract ExcelDataRowRangeIndex GetDataRowStartAndEndRowIndex(TWorkbook workbook, TSheet worksheet, ExcelImportOptions options);

        /// <summary>
        /// Gets the data row [Step 8]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="rowIndex">The row index (zero-based)</param>
        /// <returns></returns>
        protected abstract TRow GetDataRow(TWorkbook workbook, TSheet worksheet, int rowIndex);

        /// <summary>
        /// Converts the cell value [Step 9]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="dataRow">The data row</param>
        /// <param name="columnIndex">The column index (source index)</param>
        /// <param name="property">The <typeparamref name="TImportDto"/> property mapped from the header row</param>
        /// <returns></returns>
        protected abstract object? ConvertCellValue(TWorkbook workbook, TSheet worksheet, TRow dataRow, int columnIndex, PropertyInfo property);

        /// <summary>
        /// Gets the cell address, such as A1 [Step 10]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="dataRow">The data row</param>
        /// <param name="columnIndex">The column index (source index)</param>
        /// <returns></returns>
        protected abstract string GetCellAddress(TWorkbook workbook, TSheet worksheet, TRow dataRow, int columnIndex);

        #endregion
    }
}
