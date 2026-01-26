namespace MoLibrary.Core.Module.Models;

public enum EMoModuleOrder
{
    Normal = 0,
    PostConfig = 100,
    PreConfig = -100
}

public enum EMoModuleApplicationMiddlewaresOrder
{
    BeforeUseRouting = -50,
    AfterUseRouting = 50,
}

public static class ModuleOrder
{
    public const int MIDDLEWARE_USE_ROUTING = -1;
}