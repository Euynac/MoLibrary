using BuildingBlocksPlatform.DataSync.Interfaces;
using Microsoft.AspNetCore.Http;
using MoLibrary.DependencyInjection.AppInterfaces;

namespace BuildingBlocksPlatform.DataSync;

/// <summary>
/// 从请求头获取请求来自于哪个节点
/// </summary>
/// <param name="accessor"></param>
public class RequestSource(IHttpContextAccessor accessor) : IRequestSource, ITransientDependency
{
    private static readonly AsyncLocal<bool?> isSelfHosted = new();

    public string GetSource()
    {
        if (accessor.HttpContext?.Request is { } request && request.Headers.TryGetValue("X-FIPS-SOURCE", out var source))
        {
            return source!;
        }

        return "";
    }
}