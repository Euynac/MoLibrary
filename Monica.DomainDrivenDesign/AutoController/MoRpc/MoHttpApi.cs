using Monica.DependencyInjection.AppInterfaces;

namespace Monica.DomainDrivenDesign.AutoController.MoRpc;

public abstract class MoHttpApi(ICachedServiceProvider serviceProvider, HttpClient httpClient) : MoRpcApi(serviceProvider)
{
    protected readonly HttpClient HttpClient = httpClient;
}
