using Monica.Office.Excel.Models;
using Monica.StateStore.ProgressBar;

namespace Monica.Office.Excel
{
    /// <summary>
    /// Excel export service
    /// </summary>
    public interface IMoExcelExportManager
    {
        /// <summary>
        /// Gets the export header metadata
        /// </summary>
        /// <typeparam name="TExportDto">The DTO type to export</typeparam>
        /// <returns></returns>
        List<ExcelExportHeaderOutput> GetExportHeader<TExportDto>() where TExportDto : class;

        /// <summary>
        /// Exports data
        /// </summary>
        /// <typeparam name="TExportDto">The element type in <paramref name="data"/>. Columns are exported in <typeparamref name="TExportDto"/> property order.</typeparam>
        /// <param name="data">The data to export</param>
        /// <param name="requests"></param>
        /// <param name="optionAction">Configures export options</param>
        /// <param name="progressBar">Optional progress bar instance. No progress is reported when <see langword="null"/>.</param>
        /// <returns></returns>
        byte[] Export<TExportDto>(IReadOnlyList<TExportDto> data, ExcelHeaderRequest[] requests, Action<ExcelExportOptions>? optionAction = null, ProgressBar? progressBar = null)
            where TExportDto : class;

        /// <summary>
        /// Exports data asynchronously
        /// </summary>
        /// <typeparam name="TExportDto">The element type in <paramref name="data"/>. Columns are exported in <typeparamref name="TExportDto"/> property order.</typeparam>
        /// <param name="data">The data to export</param>
        /// <param name="requests"></param>
        /// <param name="optionAction">Configures export options</param>
        /// <param name="progressBar">Optional progress bar instance. No progress is reported when <see langword="null"/>.</param>
        /// <returns></returns>
        Task<byte[]> ExportAsync<TExportDto>(IReadOnlyList<TExportDto> data, ExcelHeaderRequest[] requests, Action<ExcelExportOptions>? optionAction = null, ProgressBar? progressBar = null)
            where TExportDto : class;
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
        byte[] Export<TExportDto>(IReadOnlyList<TExportDto> data, Action<ExcelExportOptions>? optionAction = null,
            string[]? onlyExportHeaderName = null, ProgressBar? progressBar = null)
            where TExportDto : class;

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
        Task<byte[]> ExportAsync<TExportDto>(IReadOnlyList<TExportDto> data,
            Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null,
            ProgressBar? progressBar = null) where TExportDto : class;
    }
}
