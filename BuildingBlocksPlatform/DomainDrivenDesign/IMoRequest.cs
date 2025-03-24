using BuildingBlocksPlatform.SeedWork;
using MediatR;
using MoLibrary.Tool.MoResponse;

namespace BuildingBlocksPlatform.DomainDrivenDesign;

/// <summary>
/// 请求类接口
/// </summary>
/// <typeparam name="TRequest">相应请求的响应类</typeparam>
public interface IMoRequest<TRequest> : IRequest<Res<TRequest>>
{
    
}