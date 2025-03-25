using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Features.MoMapper;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.DomainDrivenDesign.Interfaces;

namespace MoLibrary.DomainDrivenDesign;

public abstract class MoApplicationService :
    IMoServiceProviderInjector, IMoApplicationService,
    ITransientDependency
{

    public IMoMapper ObjectMapper => MoProvider.ServiceProvider.GetRequiredService<IMoMapper>();

    public IMoServiceProvider MoProvider { get; set; }

    public IServiceProvider ServiceProvider => MoProvider.ServiceProvider;

}
public abstract class MoApplicationService<THandler, TRequest, TResponse> :
    MoApplicationService, IRequestHandler<TRequest, TResponse>
    where THandler : MoApplicationService where TRequest : IRequest<TResponse>
{

    public abstract Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
