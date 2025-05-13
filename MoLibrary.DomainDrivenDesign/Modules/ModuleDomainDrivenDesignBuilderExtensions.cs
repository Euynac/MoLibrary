using Microsoft.AspNetCore.Builder;

namespace MoLibrary.DomainDrivenDesign.Modules;

public static class ModuleDomainDrivenDesignBuilderExtensions
{
    public static ModuleDomainDrivenDesignGuide AddMoModuleDomainDrivenDesign(this WebApplicationBuilder builder, Action<ModuleDomainDrivenDesignOption>? action = null)
    {
        return new ModuleDomainDrivenDesignGuide().Register(action);
    }
}