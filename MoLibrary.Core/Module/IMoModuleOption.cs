namespace MoLibrary.Core.Module;

public interface IMoModuleOption
{

}


public interface IMoModuleOption<TModule> : IMoModuleOption where TModule : IMoModule
{

}