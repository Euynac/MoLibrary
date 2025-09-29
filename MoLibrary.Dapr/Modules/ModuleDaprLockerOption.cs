using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Dapr.Modules;

public class ModuleDaprLockerOption : MoModuleOption<ModuleDaprLocker>
{
    public string StoreName { get; set; } = default!;

    public string? OwnerPrefix { get; set; }

    public TimeSpan DefaultExpirationTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
