using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Repository.Modules;

public class ModuleUnitOfWorkOption : IMoModuleOption<ModuleUnitOfWork>
{

    /// <summary>
    /// 开启实体变更事件支持
    /// </summary>
    public bool EnableEntityEvent { get; set; }
}