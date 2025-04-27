using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

namespace MoLibrary.RegisterCentre.Features;

public static class TimeKeeperExtensions
{
    /// <summary>
    /// 供注册中心使用
    /// </summary>
    /// <param name="app"></param>
    /// <param name="groupName"></param>
    public static void UseEndpointsMoTimekeeperDashboard(this IApplicationBuilder app, string? groupName = "注册中心")
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = groupName, Description = "Timekeeper基础功能" }
            };
            endpoints.MapGet("/centre/timekeeper/status", async (HttpResponse response, HttpContext context, [FromServices] IRegisterCentreServer centreServer) =>
            {
                var dict = await centreServer.GetAsync<object>("/timekeeper/status");
                return dict.Select(p => new
                {
                    p.Key.AppId,
                    p.Value
                });

            }).WithName("批量获取Timekeeper统计状态").WithOpenApi(operation =>
            {
                operation.Summary = "批量获取Timekeeper统计状态";
                operation.Description = "批量获取Timekeeper统计状态";
                operation.Tags = tagGroup;
                return operation;
            });
        });
    }
}