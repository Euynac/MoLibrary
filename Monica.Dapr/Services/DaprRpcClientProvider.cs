using Dapr.Client;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Monica.Modules;

namespace Monica.Dapr.Services;

public class DaprRpcClientProvider(
    IOptions<ModuleDaprRpcClientOption> rpcClientOptionAccessor,
    IOptions<ModuleDaprClientOption> daprClientOptionAccessor) : IRpcHttpClientRegisterProvider
{
    public void ConfigureHttpClientFactoryOptions(HttpClientFactoryOptions options, string appid)
    {
        options.HttpClientActions.Add(client =>
        {
            try
            {
                client.BaseAddress = new Uri($"http://{appid}");
            }
            catch (UriFormatException inner)
            {
                throw new ArgumentException("The appId must be a valid hostname.", nameof(appid), inner);
            }

            client.Timeout = rpcClientOptionAccessor.Value.Timeout;
        });

        if (daprClientOptionAccessor.Value.MaxReceiveMessageSize is { } size)
        {
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                builder.PrimaryHandler = new SocketsHttpHandler
                {
                    MaxResponseHeadersLength = size / 1024,
                };
            });
        }

        options.HttpMessageHandlerBuilderActions.Add(builder =>
        {
            builder.AdditionalHandlers.Add(new InvocationHandler
            {
                DefaultAppId = appid
            });
        });
    }
}
