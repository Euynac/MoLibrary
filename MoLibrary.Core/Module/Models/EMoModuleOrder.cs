namespace MoLibrary.Core.Module.Models;

public enum EMoModuleOrder
{
    Normal = 0,
    PostConfig = 100,
    PreConfig = -100
}

public enum EMoModuleApplicationMiddlewaresOrder
{
    BeforeUseRouting = -2,
    AfterUseRouting = 0,
}

public static class ModuleOrder
{
    public static int MiddlewareUseRouting = -1;
}