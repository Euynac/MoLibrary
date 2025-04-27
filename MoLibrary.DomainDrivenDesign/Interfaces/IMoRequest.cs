using MediatR;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DomainDrivenDesign.Interfaces;
/// <summary>
/// 请求类基类，请不要使用此类型
/// </summary>
public interface IMoRequestBase
{

}

/// <summary>
/// 请求类接口
/// </summary>
/// <typeparam name="TRequest">相应请求的响应类</typeparam>
public interface IMoRequest<TRequest> : IRequest<Res<TRequest>>, IMoRequestBase
{
    
}

/// <summary>
/// 简单请求类接口
/// </summary>
public interface IMoRequest : IRequest<Res>, IMoRequestBase
{

}

/// <summary>
/// 自定义请求类接口
/// </summary>
/// <typeparam name="TRequest"></typeparam>
public interface IMoCustomRequest<out TRequest> : IRequest<TRequest>
{

}