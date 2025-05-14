namespace MoLibrary.Core.Module.Interfaces;

public class MoModuleControllerOption<TModule> : MoModuleOption<TModule>, IMoModuleControllerOption where TModule : IMoModule
{
    public string? SwaggerTag { get; set; }

    public string RoutePrefix { get; set; } = nameof(TModule);
    public bool EnableControllers { get; set; }
    public string GetSwaggerGroupName() => SwaggerTag ?? nameof(TModule);
}