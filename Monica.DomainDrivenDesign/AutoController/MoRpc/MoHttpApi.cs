using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Monica.Authority.Security;
using Monica.DependencyInjection.AppInterfaces;

namespace Monica.DomainDrivenDesign.AutoController.MoRpc;

public abstract class MoHttpApi : MoRpcApi
{
    protected readonly HttpClient HttpClient;

    protected IMoSystemUserManager SystemUserManager => CachedServiceProvider.GetRequiredService<IMoSystemUserManager>();
    protected IHttpContextAccessor HttpContextAccessor => CachedServiceProvider.GetRequiredService<IHttpContextAccessor>();

    protected MoHttpApi(ICachedServiceProvider serviceProvider, HttpClient httpClient) : base(serviceProvider)
    {
        HttpClient = httpClient;
        //传递Header
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (HttpClient.DefaultRequestHeaders.Authorization is null)
        {
            //请求从后端发起
            if (HttpContextAccessor.HttpContext is null)
            {
                var token = SystemUserManager.GetTokenOfCurSystemUser();
                HttpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);
            }


            //请求来源于前端
            if (HttpContextAccessor.HttpContext?.Request.Headers.Authorization is { } authorization &&
                !string.IsNullOrWhiteSpace(authorization.ToString()))
            {
                //_httpClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, [authorization]);
                HttpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authorization!);
            }
        }

        // ReSharper disable once VirtualMemberCallInConstructor
        ModifyHttpClient(HttpClient);
    }

    protected virtual void ModifyHttpClient(HttpClient httpClient)
    {
    }
}