using System.ComponentModel;

namespace Monica.Office.Excel.Services.Support
{
    /// <summary>
    /// Converts raw Excel cell values into target CLR types.
    /// </summary>
    internal static class ExcelCellValueConverter
    {
        /// <summary>
        /// Determines whether the provided type should be treated as a numeric type in Excel.
        /// </summary>
        /// <param name="type">The target CLR type.</param>
        /// <returns></returns>
        public static bool IsNumeric(this Type type)
        {
            return type == typeof(decimal) || type == typeof(int) || type == typeof(float)
                  || type == typeof(long) || type == typeof(sbyte) || type == typeof(short)
                  || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort)
                  || type == typeof(decimal?) || type == typeof(int?) || type == typeof(float?)
                  || type == typeof(long?) || type == typeof(sbyte?) || type == typeof(short?)
                  || type == typeof(uint?) || type == typeof(ulong?) || type == typeof(ushort?);
        }

        /// <summary>
        /// Determines whether the provided type is a date/time type.
        /// </summary>
        /// <param name="type">The target CLR type.</param>
        /// <returns></returns>
        public static bool IsDateTime(this Type type)
        {
            return type == typeof(DateTime) || type == typeof(DateTime?);
        }

        /// <summary>
        /// Determines whether the provided type is a time span type.
        /// </summary>
        /// <param name="type">The target CLR type.</param>
        /// <returns></returns>
        public static bool IsTimeSpan(this Type type)
        {
            return type == typeof(TimeSpan) || type == typeof(TimeSpan?);
        }

        /// <summary>
        /// Determines whether the provided type is a Boolean type.
        /// </summary>
        /// <param name="type">The target CLR type.</param>
        /// <returns></returns>
        public static bool IsBool(this Type type)
        {
            return type == typeof(bool) || type == typeof(bool?);
        }

        /// <summary>
        /// Converts a raw Excel cell value into the requested target type.
        /// </summary>
        /// <param name="cellValue">The raw cell value.</param>
        /// <param name="valueType">The target CLR type.</param>
        /// <returns></returns>
        public static object? ConvertCellValue(this object? cellValue, Type valueType)
        {
            if (string.IsNullOrWhiteSpace(cellValue?.ToString()))
            {
                return cellValue;
            }

            if (valueType.IsDateTime())
            {
                cellValue = cellValue.GetTypedValue<DateTime>();
            }
            else if (valueType.IsTimeSpan())
            {
                cellValue = cellValue.GetTypedValue<TimeSpan>();
            }

            var text = cellValue?.ToString();
            return text == null ? cellValue : TypeDescriptor.GetConverter(valueType).ConvertFromInvariantString(text);
        }

        /// <summary>
        /// Converts the raw Excel cell value into a specific target value type.
        /// </summary>
        /// <typeparam name="T">The target value type.</typeparam>
        /// <param name="value">The raw cell value.</param>
        /// <returns></returns>
        public static T GetTypedValue<T>(this object? value) where T : struct
        {
            if (value == null)
            {
                return default;
            }

            var sourceType = value.GetType();
            var targetType = typeof(T);
            var nullableTargetType = !targetType.IsGenericType || targetType.GetGenericTypeDefinition() != typeof(Nullable<>)
                ? null
                : Nullable.GetUnderlyingType(targetType);

            if (sourceType == targetType || sourceType == nullableTargetType)
            {
                return (T)value;
            }

            if (nullableTargetType != null && sourceType == typeof(string) && ((string)value).Trim() == string.Empty)
            {
                return default;
            }

            var conversionType = nullableTargetType ?? targetType;
            if (conversionType == typeof(DateTime))
            {
                if (value is double dateNumber)
                {
                    return (T)(ValueType)DateTime.FromOADate(dateNumber);
                }

                if (sourceType == typeof(TimeSpan))
                {
                    return (T)(ValueType)new DateTime(((TimeSpan)value).Ticks);
                }

                if (sourceType == typeof(string))
                {
                    return (T)(ValueType)DateTime.Parse((string)value);
                }
            }
            else if (conversionType == typeof(TimeSpan))
            {
                if (value is double timeNumber)
                {
                    return (T)(ValueType)new TimeSpan(DateTime.FromOADate(timeNumber).Ticks);
                }

                if (sourceType == typeof(DateTime))
                {
                    return (T)(ValueType)new TimeSpan(((DateTime)value).Ticks);
                }

                if (sourceType == typeof(string))
                {
                    return (T)(ValueType)TimeSpan.Parse((string)value);
                }
            }

            return (T)Convert.ChangeType(value, conversionType);
        }
    }
}
