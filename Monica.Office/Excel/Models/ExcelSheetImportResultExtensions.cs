namespace Monica.Office.Excel.Models
{
    public static class ExcelSheetImportResultExtensions
    {
        /// <summary>
        /// Gets the error message.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static string? GetErrorMessage<T>(this ExcelImportRowResult<T>? output) where T : class, new()
        {
            if (output == null || output.Errors.Count == 0)
            {
                return null;
            }

            return $"行编号【{output.RowNum}】存在错误：{string.Join(",", output.Errors.Select(a => a.ErrorMessage))};\r\n";
        }

        /// <summary>
        /// Checks for errors and throws an exception.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static void CheckError<T>(this ExcelImportRowResult<T>? output) where T : class, new()
        {
            if (output == null)
            {
                return;
            }
            if (!output.IsValid)
            {
                throw new Exception($"{output.GetErrorMessage()}");
            }
        }

        /// <summary>
        /// Gets invalid data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static IEnumerable<T>? GetInvalidData<T>(this IEnumerable<ExcelImportRowResult<T>>? output) where T : class, new()
        {
            return output?.GetData(false);
        }

        /// <summary>
        /// Gets valid data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static IEnumerable<T>? GetValidData<T>(this IEnumerable<ExcelImportRowResult<T>>? output) where T : class, new()
        {
            return output?.GetData(true);
        }

        /// <summary>
        /// Gets all data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static IEnumerable<T>? GetAllData<T>(this IEnumerable<ExcelImportRowResult<T>>? output) where T : class, new()
        {
            return output?.GetData(null);
        }

        /// <summary>
        /// Gets the error message.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static string? GetErrorMessage<T>(this ExcelSheetImportResult<T>? output) where T : class, new()
        {
            if (output == null || output.InvalidCount <= 0)
            {
                return null;
            }
            return $"工作表【{output.SheetName}】数据错误：\r\n{string.Join(" ", output.Rows.Select(a => a.GetErrorMessage()).Where(a => a != null))}\r\n";
        }

        /// <summary>
        /// Checks for errors and throws an exception.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static void CheckError<T>(this ExcelSheetImportResult<T>? output) where T : class, new()
        {
            if (output == null)
            {
                return;
            }

            if (output.InvalidCount > 0)
            {
                throw new Exception(output.GetErrorMessage());
            }
        }

        /// <summary>
        /// Gets invalid data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static IEnumerable<T>? GetInvalidData<T>(this ExcelSheetImportResult<T>? output) where T : class, new()
        {
            return output?.GetData(false);
        }

        /// <summary>
        /// Gets valid data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static IEnumerable<T>? GetValidData<T>(this ExcelSheetImportResult<T>? output) where T : class, new()
        {
            return output?.GetData(true);
        }

        /// <summary>
        /// Gets all data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static IEnumerable<T>? GetAllData<T>(this ExcelSheetImportResult<T>? output) where T : class, new()
        {
            return output?.GetData(null);
        }


        /// <summary>
        /// Gets the error message.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static string? GetErrorMessage<T>(this IEnumerable<ExcelSheetImportResult<T>>? output) where T : class, new()
        {
            if (output == null)
            {
                return null;
            }

            if (!output.Any(a => a.InvalidCount > 0))
            {
                return null;
            }

            return string.Join(" ", output.Where(a => a.InvalidCount > 0).Select(a => a.GetErrorMessage()));
        }

        /// <summary>
        /// Checks for errors and throws an exception.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static void CheckError<T>(this IEnumerable<ExcelSheetImportResult<T>>? output) where T : class, new()
        {
            if (output == null)
            {
                return;
            }
            if (output.Any(a => a.InvalidCount > 0))
            {
                throw new Exception(output.GetErrorMessage());
            }
        }

        /// <summary>
        /// Gets invalid data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static IEnumerable<T>? GetInvalidData<T>(this IEnumerable<ExcelSheetImportResult<T>>? output) where T : class, new()
        {
            return output?.GetData(false);
        }
        /// <summary>
        /// Gets valid data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static IEnumerable<T>? GetValidData<T>(this IEnumerable<ExcelSheetImportResult<T>>? output) where T : class, new()
        {
            return output?.GetData(true);
        }
        /// <summary>
        /// Gets all data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <returns></returns>
        public static IEnumerable<T>? GetAllData<T>(this IEnumerable<ExcelSheetImportResult<T>>? output) where T : class, new()
        {
            return output?.GetData(null);
        }

        #region Private

        /// <summary>
        /// Gets data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <param name="isValid">Whether the data is valid. <see langword="null"/> returns all data.</param>
        /// <returns></returns>
        public static T? GetData<T>(this ExcelImportRowResult<T>? output, bool? isValid = true) where T : class, new()
        {
            if (output == null)
            {
                return null;
            }

            if (isValid == null || output.IsValid == isValid)
            {
                return output.Row;
            }

            return null;
        }

        /// <summary>
        /// Gets data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <param name="isValid">Whether the data is valid. <see langword="null"/> returns all data.</param>
        /// <returns></returns>
        private static IEnumerable<T>? GetData<T>(this IEnumerable<ExcelImportRowResult<T>> output, bool? isValid) where T : class, new()
        {
            return output.Select(a => a.GetData(isValid)).OfType<T>();
        }
        /// <summary>
        /// Gets data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <param name="isValid">Whether the data is valid. <see langword="null"/> returns all data.</param>
        /// <returns></returns>
        private static IEnumerable<T>? GetData<T>(this ExcelSheetImportResult<T>? output, bool? isValid) where T : class, new()
        {
            return output?.Rows?.GetData(isValid);
        }
        /// <summary>
        /// Gets data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <param name="isValid">Whether the data is valid. <see langword="null"/> returns all data.</param>
        /// <returns></returns>
        private static IEnumerable<T>? GetData<T>(this IEnumerable<ExcelSheetImportResult<T>>? output, bool? isValid) where T : class, new()
        {
            return output?.SelectMany(a => a.Rows).GetData(isValid);
        }
        #endregion
    }
}
