using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre;

public abstract class MoRegisterCentreServerConnectorBase(IRegisterCentreClient client, ILogger<MoRegisterCentreServerConnectorBase> logger, IOptions<ModuleRegisterCentreOption> option) : IRegisterCentreServerConnector
{
    public abstract Task<Res> Register(RegisterServiceStatus req);

    public virtual Task<Res> Heartbeat(RegisterServiceStatus req)
    {
        return Register(req);
    }
    public virtual async Task DoingRegister()
    {
        await Task.Delay(3000);
        var retryTimes = option.Value.ClientRetryTimes;
        while (retryTimes > 0)
        {
            try
            {
                var res = await Register(client.GetServiceStatus());
                if (!res.IsOk())
                {
                    logger?.LogError(res);
                }
                else
                {
                    DoingHeartbeat();
                    break;
                }
            }
            catch (Exception e)
            {
                //logger?.LogError(e, "注册配置中心出现异常");
            }
            finally
            {
                await Task.Delay(option.Value.RetryDuration*1000);
                retryTimes--;
            }
        }
    }

    public virtual async void DoingHeartbeat()
    {
        await Task.Delay(3000);
        while (true)
        {
            try
            {
                var res = await Heartbeat(client.GetServiceStatus());
                if (!res.IsOk())
                {
                    logger?.LogError(res);
                }
            }
            catch (Exception e)
            {
                //logger?.LogError(e, "向配置中心发送心跳出现异常");
            }
            finally
            {
                await Task.Delay(option.Value.HeartbeatDuration*1000);
            }
        }
    }
}