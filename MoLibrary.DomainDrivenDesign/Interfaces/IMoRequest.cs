using MediatR;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DomainDrivenDesign.Interfaces;

/// <summary>
/// 请求类接口
/// </summary>
/// <typeparam name="TRequest">相应请求的响应类</typeparam>
public interface IMoRequest<TRequest> : IRequest<Res<TRequest>>
{
    
}

/// <summary>
/// 简单请求类接口
/// </summary>
public interface IMoRequest : IRequest<Res>
{

}

/// <summary>
/// 自定义请求类接口
/// </summary>
/// <typeparam name="TRequest"></typeparam>
[Obsolete("自动生成的接口未实现支持诸如文件返回的类型")]
public interface IMoCustomRequest<out TRequest> : IRequest<TRequest>
{

}