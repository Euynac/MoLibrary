using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection;
using Monica.DomainDrivenDesign.AutoController.Interfaces;

namespace Monica.DomainDrivenDesign.AutoController.Components;

public class MoServiceConventionWrapper(IServiceProvider services) : IApplicationModelConvention
{
    private readonly Lazy<IMoServiceConvention> _convention = new(services.GetRequiredService<IMoServiceConvention>);

    public void Apply(ApplicationModel application)
    {
        _convention.Value.Apply(application);
    }
}
