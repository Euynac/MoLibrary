using System.Net.Http.Headers;
using BuildingBlocksPlatform.Authority.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;


namespace BuildingBlocksPlatform.SeedWork;


public interface IOurRpcApi
{

}

public abstract class OurRpcApi(IMoServiceProvider provider)
{

    public IMoServiceProvider MoProvider { get; set; } = provider;

    public IServiceProvider ServiceProvider => MoProvider.ServiceProvider;
    protected IMoSystemUserManager _system => ServiceProvider.GetRequiredService<IMoSystemUserManager>()!;
    protected IHttpContextAccessor _accessor => ServiceProvider.GetRequiredService<IHttpContextAccessor>()!;

}

public class OurHttpApi : OurRpcApi
{
    protected readonly HttpClient _httpClient;
  
    public OurHttpApi(IMoServiceProvider provider, HttpClient httpClient) : base(provider)
    {
        _httpClient = httpClient;
        //传递Header
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (ServiceProvider != null && _httpClient.DefaultRequestHeaders.Authorization is null)
        {
            //请求从后端发起
            if (_accessor.HttpContext is null)
            {
                var token = _system.GetTokenOfSystemUser();
                _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);
            }

            //请求来源于前端
            if (_accessor.HttpContext?.Request.Headers.Authorization is { } authorization &&
                !string.IsNullOrWhiteSpace(authorization.ToString()))
            {
                //_httpClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, [authorization]);
                _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authorization!);
            }
        }
        
    }
}