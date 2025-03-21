using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace BuildingBlocksPlatform.DataChannel.Dashboard;

public static class DataChannelDashboardBuilderExtensions
{
    /// <summary>
    /// 使用DataChannelDashboard中间件
    /// </summary>
    /// <param name="app"></param>
    /// <param name="groupName"></param>
    public static void UseDataChannelDashboard(this IApplicationBuilder app, string? groupName = "DataChannel")
    {
        app.UseEndpoints(endpoints => AddDataChannelApi(endpoints, groupName ?? "DataChannel"));
    }


    internal static void AddDataChannelApi(IEndpointRouteBuilder endpoints, string groupName)
    {


        //var tagGroup = new List<OpenApiTag> {new() {Name = groupName, Description = "DataChannel相关接口"}};
        ////获取Channel列表
        //endpoints.MapGet("/data-channel/channels", async (HttpResponse response, HttpContext context) =>
        //{
        //    var channels = DataChannelCentral.Channels.Select(p => p.Value).Select(p => new
        //    {
        //        p.Id,
        //        Middlewares = p.GetMiddlewares().Select(m => m.GetType().Name),
        //        Endpoints = p.GetEndpoints().Select(m => m.GetType().Name)
        //    });
        //    await context.Response.WriteAsJsonAsync(channels);
        //}).WithName("获取DataChannel状态列表").WithOpenApi(operation =>
        //{
        //    operation.Summary = "获取DataChannel状态列表";
        //    operation.Description = "获取DataChannel状态列表";
        //    operation.Tags = tagGroup;
        //    return operation;
        //});

        ////对某ID的DataChannel进行重新初始化操作。使用IDataChannelManager
        //endpoints.MapGet("/data-channel/channel/{id}/re-init", async (HttpResponse response, HttpContext context) =>
        //{
        //    var id = context.Request.RouteValues["id"]?.ToString();
        //    if (id == null)
        //    {
        //        await context.Response.WriteAsJsonAsync(new {Message = "id不能为空"});
        //        return;
        //    }

        //    if (DataChannelCentral.Channels.TryGetValue(id, out var channel))
        //    {
        //        await channel.InitAsync();
        //        await context.Response.WriteAsJsonAsync(new {Message = "重初始化成功"});
        //    }
        //    else
        //    {
        //        await context.Response.WriteAsJsonAsync(new {Message = "未找到对应的DataChannel"});
        //    }
        //}).WithName("对给定ID的DataChannel进行重新初始化操作").WithOpenApi(operation =>
        //{
        //    operation.Summary = "对给定ID的DataChannel进行重新初始化操作";
        //    operation.Description = "对给定ID的DataChannel进行重新初始化操作";
        //    operation.Tags = tagGroup;
        //    operation.Parameters = new List<OpenApiParameter>
        //    {
        //        new()
        //        {
        //            Name = "id",
        //            In = ParameterLocation.Path,
        //            Required = true,
        //            Description = "DataChannel ID"
        //        }
        //    };
        //    return operation;
        //});

    }
}