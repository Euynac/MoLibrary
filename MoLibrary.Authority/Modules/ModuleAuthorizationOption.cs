using MoLibrary.Core.ExceptionHandler;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Authority.Modules;

public class ModuleAuthorizationOption : MoModuleOption<ModuleAuthorization>, IMoModuleOptionUseGlobalException
{
    public bool UseGlobalExceptionHandler { get; set; }
}