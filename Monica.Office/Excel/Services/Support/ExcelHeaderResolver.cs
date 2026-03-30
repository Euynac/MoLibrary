using Monica.Office.Excel.Annotations;
using Monica.Office.Excel.Models;
using Monica.Office.Excel.Models.Internal;

namespace Monica.Office.Excel.Services.Support
{
    /// <summary>
    /// Resolves and validates export header requests for Excel DTO types.
    /// </summary>
    internal static class ExcelHeaderResolver
    {
        /// <summary>
        /// Resolves the headers to export and validates the requested header names.
        /// </summary>
        /// <typeparam name="TExportDto">The DTO type to export.</typeparam>
        /// <param name="requests">The requested headers. An empty array means all exportable headers.</param>
        /// <param name="disallowDuplicateHeader">Whether duplicate requested headers should be rejected.</param>
        /// <returns></returns>
        public static ExcelExportHeaderInfo[] ResolveHeaders<TExportDto>(
            ExcelHeaderRequest[] requests,
            bool disallowDuplicateHeader = false) where TExportDto : class
        {
            var className = typeof(TExportDto).Name;
            var properties = ExcelPropertyResolver.GetProperties<TExportDto>();

            var duplicateHeaders = properties.Select(property => property.GetDisplayName())
                .GroupBy(header => header)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .Distinct()
                .ToList();

            if (duplicateHeaders.Any())
            {
                throw new Exception($"类【{className}】中 Display Name 重复（或与属性名称重复）：{string.Join(",", duplicateHeaders)}");
            }

            if (requests.Length == 0)
            {
                var allHeaders = properties.Select(property => new ExcelExportHeaderInfo
                {
                    PropertyInfo = property,
                    HeaderName = property.GetDisplayName()
                }).ToArray();

                if (allHeaders.Length == 0)
                {
                    throw new Exception($"类【{className}】中没有要导出的表头信息");
                }

                return allHeaders;
            }

            if (disallowDuplicateHeader)
            {
                var duplicateRequests = requests.GroupBy(request => request.QueryName)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .Distinct()
                    .ToList();

                if (duplicateRequests.Any())
                {
                    throw new Exception($"指定表头名称重复：{string.Join(",", duplicateRequests)}");
                }
            }

            var headers = new List<ExcelExportHeaderInfo>();
            foreach (var request in requests)
            {
                var requestedName = request.QueryName;
                var property = properties.FirstOrDefault(candidate =>
                    candidate.Name.Equals(requestedName, StringComparison.OrdinalIgnoreCase)
                    || candidate.GetDisplayName() == requestedName);

                if (property == null)
                {
                    throw new Exception($"类【{className}】中未找到名称为【{requestedName}】的 Display Name 或属性名称，或是否已使用{nameof(IgnoreColumnAttribute)}忽略");
                }

                headers.Add(new ExcelExportHeaderInfo
                {
                    PropertyInfo = property,
                    HeaderName = request.CustomHeaderName ?? property.GetDisplayName(),
                    Option = request
                });
            }

            return headers.ToArray();
        }
    }
}
