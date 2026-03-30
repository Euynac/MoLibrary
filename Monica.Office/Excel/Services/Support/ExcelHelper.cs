using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Monica.Office.Excel.Annotations;
using Monica.Office.Excel.Models;
using Monica.Office.Excel.Models.Internal;

namespace Monica.Office.Excel.Services.Support
{
    /// <summary>
    /// Excel helper
    /// </summary>
    public static class ExcelHelper
    {
        public static string[] Extensions = [".xlsx", ".xls"];

        /// <summary>
        /// Determines whether the file is an Excel file
        /// </summary>
        /// <param name="fileName">The file name including the extension</param>
        /// <returns></returns>
        public static bool IsExcel(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var fileExt = Path.GetExtension(fileName);
            return Extensions.Contains(fileExt, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validates that the file is an Excel file and throws otherwise
        /// </summary>
        /// <param name="physicalPath">The physical path of the Excel file</param>
        /// <returns></returns>
        public static void ValidationExcel(string physicalPath)
        {
            if (string.IsNullOrWhiteSpace(physicalPath))
            {
                throw new ArgumentNullException(nameof(physicalPath), $"文件路径不能为空");
            }

            if (!File.Exists(physicalPath))
            {
                throw new FileNotFoundException($"文件不存在：{physicalPath}");
            }

            if (!IsExcel(physicalPath))
            {
                throw new Exception($"仅支持文件扩展名为 {string.Join(",", Extensions)} 的文件");
            }
        }

        /// <summary>
        /// Gets validation results
        /// </summary>
        /// <param name="instance">The object instance</param>
        /// <returns>Returns <see langword="null"/> when there are no validation errors.</returns>
        public static List<ValidationResult>? GetValidationResult(object? instance)
        {
            if (instance == null) return null;

            var valid = new List<ValidationResult>();
            var success = Validator.TryValidateObject(instance, new ValidationContext(instance), valid, true);
            if (!success)
            {
                return valid;
            }
            return null;
        }

        /// <summary>
        /// Determines whether the type is numeric
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsDouble(this Type type)
        {
            return type == typeof(decimal) || type == typeof(int) || type == typeof(float) ||
                  type == typeof(long) || type == typeof(sbyte) || type == typeof(short) ||
                  type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort)
                  || type == typeof(decimal?) || type == typeof(int?) || type == typeof(float?) ||
                  type == typeof(long?) || type == typeof(sbyte?) || type == typeof(short?) ||
                  type == typeof(uint?) || type == typeof(ulong?) || type == typeof(ushort?);
        }

        /// <summary>
        /// Determines whether the type is a date/time type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsDateTime(this Type type)
        {
            return type == typeof(DateTime) || type == typeof(DateTime?);
        }

        /// <summary>
        /// Determines whether the type is a time span type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsTimeSpan(this Type type)
        {
            return type == typeof(TimeSpan) || type == typeof(TimeSpan?);
        }

        /// <summary>
        /// Determines whether the type is a Boolean type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsBool(this Type type)
        {
            return type == typeof(bool) || type == typeof(bool?);
        }

        /// <summary>
        /// Gets the properties
        /// </summary>
        /// <typeparam name="TDto">The import or export DTO type</typeparam>
        public static PropertyInfo[] GetProperties<TDto>() where TDto : class
        {
            var dtoType = typeof(TDto);
            var properties = dtoType.GetProperties()
                .Where(a => a.GetCustomAttribute<IgnoreColumnAttribute>() == null)
                .ToArray();
            return properties;
        }

        /// <summary>
        /// Validates the headers and gets the header metadata to export
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="onlyExportHeaderName">The header names to export. If not specified, all headers are exported in <typeparamref name="TExportDto"/> property order. If specified, headers are exported in array order.</param>
        /// <param name="optionsDisallowDuplicateHeader">Whether to validate duplicate export header selections</param>
        public static ExcelExportHeaderInfo[] CheckHeader<TExportDto>(ExcelHeaderRequest[] onlyExportHeaderName,
            bool optionsDisallowDuplicateHeader = false) where TExportDto : class
        {
            var className = typeof(TExportDto).Name;

            var headers = new List<ExcelExportHeaderInfo>();

            var properties = GetProperties<TExportDto>();

            var headerDuplicate = properties.Select(a => a.GetDisplayNameFromProperty())
                .GroupBy(a => a)
                .Where(a => a.Count() > 1)
                .Select(a => a.Key).Distinct().ToList();

            if (headerDuplicate.Any())
            {
                throw new Exception(
                    $"类【{className}】中 Display Name 重复（或与属性名称重复）：{string.Join(",", headerDuplicate)}");
            }

            if (onlyExportHeaderName.LongLength == 0)
            {
                headers = properties.Select(a => new ExcelExportHeaderInfo
                {
                    PropertyInfo = a,
                    HeaderName = a.GetDisplayNameFromProperty()
                }).ToList();

                if (!headers.Any())
                {
                    throw new Exception($"类【{className}】中没有要导出的表头信息");
                }
            }
            else
            {
                if (optionsDisallowDuplicateHeader)
                {
                    var onlyDuplicate = onlyExportHeaderName
                        .GroupBy(a => a)
                        .Where(a => a.Count() > 1)
                        .Select(a => a.Key).Distinct().ToList();

                    if (onlyDuplicate.Any())
                    {
                        throw new Exception(
                            $"指定表头名称重复：{string.Join(",", onlyDuplicate)}");
                    }
                }

                foreach (var request in onlyExportHeaderName)
                {
                    var name = request.QueryName;

                    var p = properties.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || a.GetDisplayNameFromProperty() == name);
                    if (p == null)
                    {
                        throw new Exception($"类【{className}】中未找到名称为【{name}】的 Display Name 或属性名称，或是否已使用{nameof(IgnoreColumnAttribute)}忽略");
                    }

                    headers.Add(new ExcelExportHeaderInfo
                    {
                        PropertyInfo = p,
                        HeaderName = request.CustomHeaderName ?? p.GetDisplayNameFromProperty(),
                        Option = request
                    });
                }
            }

            return headers.ToArray();
        }
        /// <summary>
        /// Gets the collection of <c>Display.Name</c> values from the properties
        /// </summary>
        /// <returns></returns>
        public static List<string> GetDisplayNameListFromProperty<TDto>() where TDto : class
        {
            return GetProperties<TDto>().Select(GetDisplayNameFromProperty).ToList();
        }

        /// <summary>
        /// Gets the <c>Display.Name</c> value from the property
        /// <para>If <c>Display.Name</c> is not set, returns <c>property.Name</c>.</para>
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        public static string GetDisplayNameFromProperty(this PropertyInfo property)
        {
            return property.GetCustomAttribute<DisplayAttribute>()?.Name ?? property.Name;
        }

        /// <summary>
        /// Converts a cell value
        /// </summary>
        /// <param name="cellValue"></param>
        /// <param name="valueType"></param>
        /// <returns></returns>
        public static object? ConvertExcelCellValue(this object? cellValue, Type valueType)
        {
            if (string.IsNullOrWhiteSpace(cellValue?.ToString()))
            {
                return cellValue;
            }

            if (valueType.IsDateTime())
            {
                cellValue = cellValue.GetTypedCellValue<DateTime>();
            }
            else if (valueType.IsTimeSpan())
            {
                cellValue = cellValue.GetTypedCellValue<TimeSpan>();
            }

            var text = cellValue?.ToString();
            return text == null ? cellValue : TypeDescriptor.GetConverter(valueType).ConvertFromInvariantString(text);
        }

        /// <summary>
        /// Gets the typed cell value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static T GetTypedCellValue<T>(this object? value) where T : struct
        {
            if (value == null)
                return default;
            var type1 = value.GetType();
            var type2 = typeof(T);
            var type3 = !type2.IsGenericType || !(type2.GetGenericTypeDefinition() == typeof(Nullable<>)) ? null : Nullable.GetUnderlyingType(type2);
            if (type1 == type2 || type1 == type3)
                return (T)value;
            if (type3 != null && type1 == typeof(string) && ((string)value).Trim() == string.Empty)
                return default;
            var type4 = type3;
            if ((object?)type4 == null)
                type4 = type2;
            var conversionType = type4;
            if (conversionType == typeof(DateTime))
            {
                if (value is double d)
                    return (T)(ValueType)DateTime.FromOADate(d);
                if (type1 == typeof(TimeSpan))
                    return (T)(ValueType)new DateTime(((TimeSpan)value).Ticks);
                if (type1 == typeof(string))
                    return (T)(ValueType)DateTime.Parse((string)value);
            }
            else if (conversionType == typeof(TimeSpan))
            {
                if (value is double d)
                    return (T)(ValueType)new TimeSpan(DateTime.FromOADate(d).Ticks);
                if (type1 == typeof(DateTime))
                    return (T)(ValueType)new TimeSpan(((DateTime)value).Ticks);
                if (type1 == typeof(string))
                    return (T)(ValueType)TimeSpan.Parse((string)value);
            }
            return (T)Convert.ChangeType(value, conversionType);
        }
    }
}
