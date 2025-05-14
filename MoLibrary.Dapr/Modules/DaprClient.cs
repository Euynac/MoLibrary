using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.GlobalJson;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Dapr.Modules;


public static class ModuleDaprClientBuilderExtensions
{
    public static ModuleDaprClientGuide ConfigModuleDaprClient(this IServiceCollection services,
        Action<ModuleDaprClientOption>? action = null)
    {
        return new ModuleDaprClientGuide().Register(action);
    }
}

public class ModuleDaprClient(ModuleDaprClientOption option)
    : MoModule<ModuleDaprClient, ModuleDaprClientOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DaprClient;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {
        services.AddDaprClient(builder => builder.UseGrpcChannelOptions(new GrpcChannelOptions()
        {
            MaxReceiveMessageSize = Option.MaxReceiveMessageSize,
            MaxSendMessageSize = Option.MaxSendMessageSize,
            MaxRetryBufferSize = Option.MaxRetryBufferSize,

        }).UseJsonSerializationOptions(DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions));
        return Res.Ok();
    }
}

public class ModuleDaprClientGuide : MoModuleGuide<ModuleDaprClient, ModuleDaprClientOption, ModuleDaprClientGuide>
{


}

public class ModuleDaprClientOption : IMoModuleOption<ModuleDaprClient>
{

    /// <summary>
    /// Gets or sets the maximum message size in bytes that can be sent from the client. Attempting to send a message
    /// that exceeds the configured maximum message size results in an exception.
    /// <para>
    /// A <c>null</c> value removes the maximum message size limit. Defaults to <c>null</c>.
    /// </para>
    /// </summary>
    public int? MaxSendMessageSize { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum message size in bytes that can be received by the client. If the client receives a
    /// message that exceeds this limit, it throws an exception.
    /// <para>
    /// A <c>null</c> value removes the maximum message size limit. Defaults to 4,194,304 (4 MB).
    /// </para>
    /// </summary>
    public int? MaxReceiveMessageSize { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum buffer size in bytes that can be used to store sent messages when retrying
    /// or hedging calls. If the buffer limit is exceeded, then no more retry attempts are made and all
    /// hedging calls but one will be canceled. This limit is applied across all calls made using the channel.
    /// <para>
    /// Setting this value alone doesn't enable retries. Retries are enabled in the service config, which can be done
    /// using <see cref="P:Grpc.Net.Client.GrpcChannelOptions.ServiceConfig" />.
    /// </para>
    /// <para>
    /// A <c>null</c> value removes the maximum retry buffer size limit. Defaults to 16,777,216 (16 MB).
    /// </para>
    /// <para>
    /// Note: Experimental API that can change or be removed without any prior notice.
    /// </para>
    /// </summary>
    public long? MaxRetryBufferSize { get; set; } = 100 * 1024 * 1024;
}