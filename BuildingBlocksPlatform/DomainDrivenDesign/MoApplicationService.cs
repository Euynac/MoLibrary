using BuildingBlocksPlatform.DomainDrivenDesign.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;


namespace BuildingBlocksPlatform.DomainDrivenDesign;

public abstract class MoApplicationService :
    IMoServiceProviderInjector, IMoApplicationService,
    ITransientDependency
{

    public IMoObjectMapper ObjectMapper => MoProvider.ServiceProvider.GetRequiredService<IMoObjectMapper>();

    public IMoServiceProvider MoProvider { get; set; }

    public IServiceProvider ServiceProvider => MoProvider.ServiceProvider;

}
public abstract class MoApplicationService<THandler, TRequest, TResponse> :
    MoApplicationService, IRequestHandler<TRequest, TResponse>
    where THandler : MoApplicationService where TRequest : IRequest<TResponse>
{

    public abstract Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
