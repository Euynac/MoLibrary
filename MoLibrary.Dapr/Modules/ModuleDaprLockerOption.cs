using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Dapr.Modules;

public class ModuleDaprLockerOption : IMoModuleOption<ModuleDaprLocker>
{
    public string StoreName { get; set; } = default!;

    public string? Owner { get; set; }

    public TimeSpan DefaultExpirationTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
