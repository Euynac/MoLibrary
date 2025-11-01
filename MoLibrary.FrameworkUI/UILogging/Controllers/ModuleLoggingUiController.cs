using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.FrameworkUI.UILogging.Services;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.FrameworkUI.UILogging.Controllers;

/// <summary>
/// Logging UI 相关控制器
/// </summary>
[ApiController]
public class ModuleLoggingUiController(LoggingService loggingService) : MoModuleControllerBase
{
    [HttpGet("logging-ui/files")]
    public async Task<IActionResult> ListLogFiles()
    {
        var result = await loggingService.ListFilesAsync();
        return result.GetResponse(this);
    }

    [HttpGet("logging-ui/files/{*filePath}")]
    public async Task<IActionResult> DownloadLogFile([FromRoute] string filePath)
    {
        filePath = Uri.UnescapeDataString(filePath);
        var result = await loggingService.OpenFileAsync(filePath);
        if (result.IsFailed(out var error, out var stream))
        {
            return error.GetResponse(this);
        }

        stream.Seek(0, SeekOrigin.Begin);
        var downloadName = Path.GetFileName(filePath);
        return File(stream, "text/plain", downloadName);
    }

    [HttpGet("logging-ui/current/export")]
    public async Task<IActionResult> ExportCurrentLog()
    {
        var result = await loggingService.ExportBufferAsync();
        if (result.IsFailed(out var error, out var export))
        {
            return error.GetResponse(this);
        }

        return File(export.Content, export.ContentType, export.FileName);
    }
}
