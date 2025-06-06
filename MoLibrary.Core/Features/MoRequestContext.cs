using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Features;

public class MoRequestContext
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

        ChainBridge = ChainBridge!.Invoked($"{res.Message}({res.Code})", duration, true, CreateResExtraInfo(res));
        RefreshServiceResponse(res);
    }
    /// <summary>
    /// 更换为最新的ExtraInfo传递下去
    /// </summary>
    /// <param name="res"></param>
    private void RefreshServiceResponse(IServiceResponse? res)
    {
        if (res is { } obj)
        {
            obj.ExtraInfo ??= new ExpandoObject();
            obj.ExtraInfo.Remove("invocationChain", out _);
            ((dynamic)obj.ExtraInfo).InvocationChain = ChainBridge!;
            if (OtherInfo is { } otherInfo)
            {
                ((dynamic)obj.ExtraInfo).OtherInfo = otherInfo;
            }
        }
    }

    private object? CreateResExtraInfo(IServiceResponse? res)
    {
        //TODO 优化异常链路处理，合并到chain
        if (res == null) return null;
        var node = JsonSerializer.SerializeToNode(res.ExtraInfo);
        if (res.IsOk()) return node;

        if (node != null)
        {
            node["msg"] = res.Message;
        }
        else
        {
            return res.Message;

        }
        return node;
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


        //TODO 用更好的方式合并
        ChainBridge = ChainBridge!.Invoked(response, duration, extraInfo: CreateResExtraInfo(res));
        RefreshServiceResponse(res);
    }

}

public class InvokeChainInfo
{
    /// <summary>
    /// 是远程调用
    /// </summary>
    public bool IsRemoteInvoke { get; set; }
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
        IsRemoteInvoke = isRemote ?? false;
        InvokedExtraInfo = extraInfo;
        return PreviousChain ?? this;
    }
}