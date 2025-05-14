namespace MoLibrary.Core.Module.Models;

public enum EMoModuleOrder
{
    Normal = 0,
    PostConfig = 100,
    PreConfig = -100
}

public static class ModuleOrder
{
    public static int MiddlewareUseRouting = -1;
}