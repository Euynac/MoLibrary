using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Features;

public interface IMoDebugger
{
    public void WriteToDebugger(string msg);
    public void WriteToDebugger(string msg, params object[] args);
    public IReadOnlyList<string> GetHistories(string? filter);
}

public class MoDebugger : IMoDebugger
{
    private static readonly ConcurrentBag<string> _stores = [];

    public void WriteToDebugger(string msg)
    {
        _stores.Add($"[{DateTime.Now}]{msg}");
    }

    public void WriteToDebugger(string msg, params object[] args)
    {
        _stores.Add($"[{DateTime.Now}]{string.Format(msg, args)}");
    }

    public IReadOnlyList<string> GetHistories(string? filter)
    {
        return _stores.WhereIf(filter != null, s => s.Contains(filter!, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}

public static class MoDebuggerBuilderExtensions
{
    public static void AddMoDebugger(this IServiceCollection services)
    {
        services.AddSingleton<IMoDebugger, MoDebugger>();
    }
    public static void UseEndpointsMoDebugger(this IApplicationBuilder app, string? groupName = "Debugger")
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = groupName, Description = "Debugger基础功能" }
            };
            endpoints.MapGet("/debugger", async (HttpResponse response, HttpContext context, [FromServices] IMoDebugger debugger,[FromQuery] string? filter) =>
            {
                var res = debugger.GetHistories(filter);
                await response.WriteAsJsonAsync(res);
            }).WithName("获取当前Debugger历史").WithOpenApi(operation =>
            {
                operation.Summary = "获取当前Debugger历史";
                operation.Description = "获取当前Debugger历史";
                operation.Tags = tagGroup;
                return operation;
            });
        });
    }
}