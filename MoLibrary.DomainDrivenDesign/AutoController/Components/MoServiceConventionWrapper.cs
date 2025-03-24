using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.DomainDrivenDesign.AutoController.Interfaces;

namespace MoLibrary.DomainDrivenDesign.AutoController.Components;

public class MoServiceConventionWrapper(IServiceProvider services) : IApplicationModelConvention
{
    private readonly Lazy<IMoServiceConvention> _convention = new(services.GetRequiredService<IMoServiceConvention>);

    public void Apply(ApplicationModel application)
    {
        _convention.Value.Apply(application);
    }
}
