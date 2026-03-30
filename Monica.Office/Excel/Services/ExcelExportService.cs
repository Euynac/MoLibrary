using Monica.Office.Excel.Abstractions;
using Monica.Office.Excel.Models;
using Monica.Office.Excel.Services.Support;
using Monica.StateStore.ProgressBar;

namespace Monica.Office.Excel.Services
{
    /// <summary>
    /// Excel export service
    /// </summary>
    internal abstract class ExcelExportService : IExcelExporter
    {
        /// <summary>
        /// Initializes a new instance
        /// </summary>
        protected ExcelExportService()
        {
        }

        /// <summary>
        /// Gets the export header metadata
        /// </summary>
        /// <typeparam name="TExportDto">The DTO type to export</typeparam>
        /// <returns></returns>
        public List<ExcelExportHeaderOutput> GetExportHeader<TExportDto>() where TExportDto : class
        {
            return ExcelPropertyResolver.GetProperties<TExportDto>().Select(a => new ExcelExportHeaderOutput
            {
                HeaderName = a.GetDisplayName()
            }).ToList();
        }

        /// <summary>
        /// Validates export header requests against the export DTO.
        /// </summary>
        /// <typeparam name="TExportDto">The DTO type to export.</typeparam>
        /// <param name="requests">The requested headers. An empty array means all exportable headers.</param>
        /// <param name="disallowDuplicateHeader">Whether duplicate requested headers should be rejected.</param>
        public void ValidateHeaders<TExportDto>(ExcelHeaderRequest[] requests, bool disallowDuplicateHeader = false)
            where TExportDto : class
        {
            ExcelHeaderResolver.ResolveHeaders<TExportDto>(requests, disallowDuplicateHeader);
        }

        public byte[] Export<TExportDto>(IReadOnlyList<TExportDto> data, ExcelHeaderRequest[] requests, Action<ExcelExportOptions>? optionAction = null,
            ProgressBar? progressBar = null) where TExportDto : class
        {
            try
            {
                return ImplementExport(data, requests, optionAction, progressBar);
            }
            catch (Exception e)
            {
                progressBar?.CancelTaskAsync($"导出失败: {e.Message}").Wait();
                throw new Exception(e.Message, e);
            }
        }

        public Task<byte[]> ExportAsync<TExportDto>(IReadOnlyList<TExportDto> data, ExcelHeaderRequest[] requests, Action<ExcelExportOptions>? optionAction = null,
            ProgressBar? progressBar = null) where TExportDto : class
        {
            return Task.FromResult(Export(data, requests, optionAction, progressBar));
        }

        /// <summary>
        /// Exports data
        /// </summary>
        /// <typeparam name="TExportDto">The element type in <paramref name="data"/>. Columns are exported in <typeparamref name="TExportDto"/> property order.</typeparam>
        /// <param name="data">The data to export</param>
        /// <param name="optionAction">Configures export options</param>
        /// <param name="onlyExportHeaderName">The header names to export
        ///     <para>1. If not specified, all columns are exported in <typeparamref name="TExportDto"/> property order. If specified, columns are exported in array order.</para>
        ///     <para>2. Use <see cref="GetExportHeader{TExportDto}"/> to get available header names.</para>
        /// </param>
        /// <param name="progressBar">Optional progress bar instance. No progress is reported when <see langword="null"/>.</param>
        /// <returns></returns>
        public byte[] Export<TExportDto>(IReadOnlyList<TExportDto> data,
            Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null,
            ProgressBar? progressBar = null) where TExportDto : class
        {
            try
            {
                return Export(data,onlyExportHeaderName?.Select(p=> new ExcelHeaderRequest(p)).ToArray() ?? [], optionAction, progressBar);
            }
            catch (Exception e)
            {
                progressBar?.CancelTaskAsync($"导出失败: {e.Message}").Wait();
                throw new Exception(e.Message, e);
            }
        }

        /// <summary>
        /// Exports data asynchronously
        /// </summary>
        /// <typeparam name="TExportDto">The element type in <paramref name="data"/>. Columns are exported in <typeparamref name="TExportDto"/> property order.</typeparam>
        /// <param name="data">The data to export</param>
        /// <param name="optionAction">Configures export options</param>
        /// <param name="onlyExportHeaderName">The header names to export
        ///     <para>1. If not specified, all columns are exported in <typeparamref name="TExportDto"/> property order. If specified, columns are exported in array order.</para>
        ///     <para>2. Use <see cref="GetExportHeader{TExportDto}"/> to get available header names.</para>
        /// </param>
        /// <param name="progressBar">Optional progress bar instance. No progress is reported when <see langword="null"/>.</param>
        /// <returns></returns>
        public async Task<byte[]> ExportAsync<TExportDto>(IReadOnlyList<TExportDto> data,
            Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null,
            ProgressBar? progressBar = null) where TExportDto : class
        {
            return await Task.FromResult(Export(data, optionAction, onlyExportHeaderName, progressBar));
        }

        /// <summary>
        /// Implements the export operation
        /// </summary>
        /// <typeparam name="TExportDto">The element type in <paramref name="data"/>. Header order follows property order.</typeparam>
        /// <param name="data">The data to export</param>
        /// <param name="requests"></param>
        /// <param name="optionAction">Configures export options</param>
        /// <param name="progressBar">Optional progress bar instance</param>
        /// <returns></returns>
        protected abstract byte[] ImplementExport<TExportDto>(IReadOnlyList<TExportDto> data,
            ExcelHeaderRequest[] requests,
            Action<ExcelExportOptions>? optionAction, ProgressBar? progressBar = null)
            where TExportDto : class;
    }
}
