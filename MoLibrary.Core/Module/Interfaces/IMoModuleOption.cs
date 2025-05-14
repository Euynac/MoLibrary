namespace MoLibrary.Core.Module.Interfaces;

public interface IMoModuleOption
{

}

public interface IMoModuleOption<TModule> : IMoModuleOption where TModule : IMoModule
{

}

public interface IMoModuleExtraOption<TModule> : IMoModuleOption where TModule : IMoModule
{
}


