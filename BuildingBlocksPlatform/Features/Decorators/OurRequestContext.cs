using Google.Api;
using Koubot.Tool.Extensions;
using Koubot.Tool.General;
using Microsoft.Extensions.Logging;
using System.Dynamic;
using System.Text.Json.Serialization;
using BuildingBlocksPlatform.Extensions;
using BuildingBlocksPlatform.SeedWork;

namespace BuildingBlocksPlatform.Features.Decorators;

public class OurRequestContext
{
    //public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public InvokeChainInfo? ChainBridge { get; set; }
    public ExpandoObject? OtherInfo { get; set; }
    /// <summary>
    /// 指示开始方法调用
    /// </summary>
    /// <param name="handler"></param>
    /// <param name="request"></param>
    /// <param name="extraInfo"></param>
    public void Invoking(string handler, string request, dynamic? extraInfo = null)
    {
        ChainBridge ??= new InvokeChainInfo();
        ChainBridge = ChainBridge.Invoking(handler, request, extraInfo);
    }


    /// <summary>
    /// 指示该次远程调用已完成
    /// </summary>
    public void RemoteInvoked(IServiceResponse res, TimeSpan? duration = null)
    {
        if (ChainBridge is null)
        {
            Invoking("chainBridge doesn't existed", "<failed get HttpContext>");
        }

        ChainBridge = ChainBridge!.Invoked($"{res.Message}({res.Code})", duration, true,
            res.ExtraInfo?.GetOrDefault<string, object?>("invocationChain") ?? res.ExtraInfo?.GetOrDefault<string, object?>("InvocationChain"));
        RefreshServiceResponse(res);
    }

    private void RefreshServiceResponse(IServiceResponse? res)
    {
        if (res is { } obj)
        {
            obj.ExtraInfo ??= new ExpandoObject();
            obj.ExtraInfo.Remove("invocationChain", out _);
            ((dynamic) obj.ExtraInfo).InvocationChain = ChainBridge!;
            if (OtherInfo is { } otherInfo)
            {
                ((dynamic) obj.ExtraInfo).OtherInfo = otherInfo;
            }
        }
    }

    /// <summary>
    /// 指示该次调用已完成
    /// </summary>
    /// <param name="response"></param>
    /// <param name="duration"></param>
    /// <param name="res">传入将润色<see cref="IServiceResponse.ExtraInfo"/>字段</param>
    public void Invoked(string response = "", TimeSpan? duration = null, IServiceResponse? res = null)
    {
        if (ChainBridge is null)
        {
            Invoking("chainBridge doesn't existed", "<failed get HttpContext>");
        }

        ChainBridge = ChainBridge!.Invoked(response, duration);
        RefreshServiceResponse(res);
    }

}

public class InvokeChainInfo
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsRemoteInvoke { get; set; }
    /// <summary>
    /// 方法名
    /// </summary>
    public string? Handler { get; set; }
    /// <summary>
    /// 请求名
    /// </summary>
    public string? Request { get; set; }
    /// <summary>
    /// 响应名
    /// </summary>
    public string? Response { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Duration { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Remarks { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public dynamic? InvokingExtraInfo { get; set; }  
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public dynamic? InvokedExtraInfo { get; set; }
    public List<dynamic>? Chains { get; set; }
    [JsonIgnore]
    public InvokeChainInfo? PreviousChain { get; set; }

    private readonly DateTime _startTime = DateTime.Now;

    internal InvokeChainInfo Invoking(string handler, string request, dynamic? extraInfo = null)
    {
        if (Handler == null && Request == null)
        {
            Handler = handler;
            Request = request;
            InvokingExtraInfo = extraInfo;
            return this;
        }
        var newChain = new InvokeChainInfo
        {
            Handler = handler,
            Request = request,
            InvokingExtraInfo = extraInfo,
            PreviousChain = this
        };
        Chains ??= [];
        Chains.Add(newChain);
        return newChain;
    }

    internal InvokeChainInfo Invoked(string response = "", TimeSpan? duration = null, bool? isRemote = null, dynamic? extraInfo = null)
    {
        Response = response;
        Duration = duration?.TotalMilliseconds.Be("{0}ms", true) ?? $"{(DateTime.Now - _startTime).TotalMilliseconds}ms";
        IsRemoteInvoke = isRemote;
        InvokedExtraInfo = extraInfo;
        return PreviousChain ?? this;
    }
}