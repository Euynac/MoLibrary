using Microsoft.AspNetCore.Builder;

namespace MoLibrary.AutoModel.Modules;

public static class ModuleAutoModelBuilderExtensions
{
    public static ModuleAutoModelGuide ConfigModuleAutoModel(this WebApplicationBuilder builder, Action<ModuleAutoModelOption>? action = null)
    {
        return new ModuleAutoModelGuide().Register(action);
    }
}