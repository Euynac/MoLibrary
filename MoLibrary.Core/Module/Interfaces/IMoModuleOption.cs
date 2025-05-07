namespace MoLibrary.Core.Module.Interfaces;

public interface IMoModuleOption
{

}


public interface IMoModuleOption<TModule> : IMoModuleOption where TModule : IMoModule
{

}
public interface IMoModuleExtraOption<TModule> : IMoModuleOption<TModule> where TModule : IMoModule
{
}

public interface IMoModuleControllerOption<TModule> : IMoModuleOption where TModule : IMoModule
{
    public string? SwaggerGroupName { get; set; }

    public string GetSwaggerGroupName() => SwaggerGroupName ?? nameof(TModule);
}