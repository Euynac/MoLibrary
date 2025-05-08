using Microsoft.Extensions.Logging;
using MoLibrary.RegisterCentre.Modules;

namespace MoLibrary.RegisterCentre;

public static class MoRegisterCentreManager
{
    private static ModuleRegisterCentreOption? _setting;

    internal static ILogger Logger =>
        //如果没有初始化Logger，那么就使用ConsoleLogger
        Option.Logger ??= LoggerFactory.Create(builder =>
        {
            builder.AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddFilter("MoRegisterCentre", LogLevel.Debug)
                .AddConsole();
        }).CreateLogger("MoRegisterCentre");


    internal static ModuleRegisterCentreOption Option
    {
        get => _setting ?? throw new InvalidOperationException(
            $"Setting is not initialized in {typeof(MoRegisterCentreManager)}. Please register MoRegisterCentre first");
        set => _setting = value;
    }

    /// <summary>
    /// 是否已经确认过是Client或Server
    /// </summary>
    internal static bool HasSetServerOrClient => _setting != null;
}