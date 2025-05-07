using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.AutoModel.Modules;

public static class ModuleAutoModelBuilderExtensions
{
    public static ModuleAutoModelGuide AddMoModuleAutoModel(this IServiceCollection services, Action<ModuleAutoModelOption>? action = null)
    {
        return new ModuleAutoModelGuide().Register(action);
    }
}