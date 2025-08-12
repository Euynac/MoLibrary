using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Implements;


/// <summary>
/// 内存注册中心实现，仅支持单实例
/// </summary>
/// <param name="accessor"></param>
public class MemoryProviderForRegisterCentre(IHttpContextAccessor accessor, IRegisterCentreClientConnector connector) : IRegisterCentreServer
{
    public static ConcurrentDictionary<string, ServiceRegisterInfo> Dict { get; protected set; } = new();

    public virtual async Task<Res<List<ServiceRegisterInfo>>> GetServicesStatus()
    {
        return Dict.Values.ToList();
    }

    public virtual async Task<Res> UnregisterAll()
    {
        Dict.Clear();
        return Res.Ok();
    }

    public virtual async Task<Dictionary<ServiceRegisterInfo, Res<TResponse>>> GetAsync<TResponse>(string callbackUrl)
    {
        var dict = await connector.GetAsync<TResponse>(Dict.Keys.ToList(), callbackUrl);
        return dict.ToDictionary(p => Dict[p.Key], p => p.Value);
    }

    public virtual async Task<Dictionary<ServiceRegisterInfo, Res<TResponse>>> PostAsync<TRequest, TResponse>(string callbackUrl, TRequest req)
    {
        var dict = await connector.PostAsync<TRequest, TResponse>(Dict.Keys.ToList(), callbackUrl, req);
        return dict.ToDictionary(p => Dict[p.Key], p => p.Value);
    }


    public virtual async Task<Res> Register(ServiceRegisterInfo req)
    {
        var status = req;

        if (status.AppId.IsNullOrWhiteSpace()) return Res.Fail("该微服务未设置APPID，无法注册");

        if (req.FromClient is null && accessor.HttpContext?.Connection is { } connection)
        {
            req.FromClient =
                $"[Remote: {connection.RemoteIpAddress?.ToString()}:{connection.RemotePort}][Local: {connection.LocalIpAddress?.ToString()}:{connection.LocalPort}";
        }

        Dict.AddOrUpdate(status.AppId, status, (_, _) => status);

        return Res.Ok();
    }

    public virtual Task<Res> Heartbeat(ServiceRegisterInfo req)
    {
        return Register(req);
    }
}