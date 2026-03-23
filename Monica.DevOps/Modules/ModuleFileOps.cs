using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DevOps.FileOps.Abstractions;
using Monica.DevOps.FileOps.Facades;
using Monica.DevOps.Localization;
using Monica.DevOps.FileOps.Models;
using Monica.DevOps.FileOps.Services;
using Monica.DevOps.FileOps.Services.Support;
using Monica.Tool.MoResponse;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

[ModuleKey(EMoModuleKey.FileOps)]
public class ModuleFileOps(ModuleFileOpsOption option)
    : MoModule<ModuleFileOps, ModuleFileOpsOption, ModuleFileOpsGuide>(option)
{
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<FileOpsResource>();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IFileOpsRuntimeConfigStore, FileOpsRuntimeConfigStore>();
        services.AddSingleton<FileOpsPathPolicy>();
        services.AddSingleton<FileOpsTextInspector>();
        services.AddScoped<FileOpsMessageLocalizer>();
        services.AddScoped<FileOpsWorkspaceService>();
        services.AddScoped<FileOpsTransferService>();
        services.AddScoped<FileOpsFacade>();
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            endpoints.MapGet("/file-ops/runtime-config",
                    async ([FromServices] FileOpsFacade facade, CancellationToken cancellationToken) =>
                        (await facade.GetRuntimeConfigAsync(cancellationToken)).GetResponse())
                .WithName("GetFileOpsRuntimeConfig")
                .WithTags(tagName)
                .WithSummary("Gets the current FileOps runtime configuration")
                .WithDescription("Returns the current runtime configuration used by the mixed Monica FileOps module.");

            endpoints.MapPut("/file-ops/runtime-config",
                    async ([FromBody] FileOpsRuntimeConfig runtimeConfig,
                        [FromServices] FileOpsFacade facade,
                        CancellationToken cancellationToken) =>
                        (await facade.SaveRuntimeConfigAsync(runtimeConfig, cancellationToken)).GetResponse())
                .WithName("UpdateFileOpsRuntimeConfig")
                .WithTags(tagName)
                .WithSummary("Updates the current FileOps runtime configuration")
                .WithDescription("Replaces the in-memory runtime configuration that constrains allowed roots and file operation limits.");

            endpoints.MapGet("/file-ops/browse",
                    async ([FromQuery] string? path,
                        [FromServices] FileOpsFacade facade,
                        CancellationToken cancellationToken) =>
                        (await facade.BrowseAsync(path, cancellationToken)).GetResponse())
                .WithName("BrowseFileOpsPath")
                .WithTags(tagName)
                .WithSummary("Browses a directory inside the allowed roots")
                .WithDescription("Lists directories and files for the requested path while enforcing the current FileOps runtime policy.");

            endpoints.MapGet("/file-ops/text-content",
                    async ([FromQuery] string path,
                        [FromServices] FileOpsFacade facade,
                        CancellationToken cancellationToken) =>
                        (await facade.ReadTextFileAsync(path, cancellationToken)).GetResponse())
                .WithName("GetFileOpsTextContent")
                .WithTags(tagName)
                .WithSummary("Reads a text file inside the allowed roots")
                .WithDescription("Loads a text file for emergency editing after validating the size limit and text encoding heuristics.");

            endpoints.MapPut("/file-ops/text-content",
                    async ([FromBody] FileOpsTextUpdateRequest request,
                        [FromServices] FileOpsFacade facade,
                        CancellationToken cancellationToken) =>
                        (await facade.SaveTextFileAsync(request, cancellationToken)).GetResponse())
                .WithName("UpdateFileOpsTextContent")
                .WithTags(tagName)
                .WithSummary("Saves a text file")
                .WithDescription("Writes a text file back to disk when the current runtime policy allows editing.");

            endpoints.MapPost("/file-ops/directories",
                    async ([FromBody] FileOpsCreateDirectoryRequest request,
                        [FromServices] FileOpsFacade facade,
                        CancellationToken cancellationToken) =>
                        (await facade.CreateDirectoryAsync(request, cancellationToken)).GetResponse())
                .WithName("CreateFileOpsDirectory")
                .WithTags(tagName)
                .WithSummary("Creates a directory")
                .WithDescription("Creates a new directory under the requested parent path when the current runtime policy allows writes.");

            endpoints.MapPost("/file-ops/delete",
                    async ([FromBody] FileOpsDeleteRequest request,
                        [FromServices] FileOpsFacade facade,
                        CancellationToken cancellationToken) =>
                        (await facade.DeleteEntryAsync(request, cancellationToken)).GetResponse())
                .WithName("DeleteFileOpsEntry")
                .WithTags(tagName)
                .WithSummary("Deletes a file system entry")
                .WithDescription("Deletes a file or directory when the current runtime policy allows destructive operations.");

            endpoints.MapPost("/file-ops/upload",
                    async ([FromQuery] string directoryPath,
                        [FromQuery] bool overwrite,
                        HttpRequest request,
                        [FromServices] FileOpsFacade facade,
                        CancellationToken cancellationToken) =>
                    {
                        var form = await request.ReadFormAsync(cancellationToken);
                        var payloads = new List<FileOpsUploadPayload>(form.Files.Count);
                        try
                        {
                            foreach (var formFile in form.Files)
                            {
                                payloads.Add(new FileOpsUploadPayload(formFile.FileName, formFile.Length, formFile.OpenReadStream()));
                            }

                            return (await facade.UploadAsync(directoryPath, payloads, overwrite, cancellationToken)).GetResponse();
                        }
                        finally
                        {
                            foreach (var payload in payloads)
                            {
                                payload.Content.Dispose();
                            }
                        }
                    })
                .DisableAntiforgery()
                .WithName("UploadFileOpsFiles")
                .WithTags(tagName)
                .WithSummary("Uploads one or more files")
                .WithDescription("Writes uploaded files into the target directory after validating the current runtime file operation policy.");

            endpoints.MapGet("/file-ops/download",
                    DownloadAsync)
                .WithName("DownloadFileOpsEntry")
                .WithTags(tagName)
                .WithSummary("Downloads a file")
                .WithDescription("Streams a file from an allowed root when the current runtime policy permits the requested download size.");
        });
    }

    private static async Task<IResult> DownloadAsync(
        [FromQuery] string path,
        [FromServices] FileOpsTransferService transferService,
        [FromServices] FileOpsMessageLocalizer messageLocalizer,
        [FromServices] IStringLocalizer<FileOpsResource> localizer,
        [FromServices] ILogger<ModuleFileOps> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var descriptor = await transferService.OpenDownloadAsync(path, cancellationToken);
            return Results.File(
                descriptor.ContentStream,
                descriptor.ContentType,
                descriptor.FileName,
                lastModified: descriptor.LastModifiedAt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download FileOps file {Path}.", path);
            var response = Res.Fail(
                localizer["ServiceMessages:DownloadFailed", path, messageLocalizer.TranslateExceptionMessage(ex)].Value,
                GetResponseCode(ex));
            return response.GetResponse();
        }
    }

    private static ResponseCode GetResponseCode(Exception exception)
    {
        return exception switch
        {
            ArgumentException => ResponseCode.BadRequest,
            InvalidOperationException => ResponseCode.BadRequest,
            KeyNotFoundException => ResponseCode.BadRequest,
            DirectoryNotFoundException => ResponseCode.BadRequest,
            FileNotFoundException => ResponseCode.BadRequest,
            IOException => ResponseCode.BadRequest,
            UnauthorizedAccessException => ResponseCode.Forbidden,
            Monica.DevOps.FileOps.Exceptions.FileOpsOperationException => ResponseCode.BadRequest,
            _ => ResponseCode.InternalError
        };
    }
}

public static class ModuleFileOpsBuilderExtensions
{
    extension(Mo)
    {
        public static ModuleFileOpsGuide AddFileOps(Action<ModuleFileOpsOption>? action = null)
        {
            return new ModuleFileOpsGuide().Register(action);
        }
    }
}

public class ModuleFileOpsGuide : MoModuleGuide<ModuleFileOps, ModuleFileOpsOption, ModuleFileOpsGuide>
{
}

public class ModuleFileOpsOption : MoModuleOptionWithMinimalApi<ModuleFileOps>
{
    public FileOpsRuntimeConfig RuntimeConfig { get; set; } = new();
}
