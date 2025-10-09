using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Authority.Security;
using MoLibrary.DependencyInjection.AppInterfaces;

namespace MoLibrary.DomainDrivenDesign.AutoController.MoRpc;

public abstract class MoHttpApi : MoRpcApi
{
    protected readonly HttpClient HttpClient;

    protected IMoSystemUserManager SystemUserManager => ServiceProvider.GetRequiredService<IMoSystemUserManager>()!;
    protected IHttpContextAccessor HttpContextAccessor => ServiceProvider.GetRequiredService<IHttpContextAccessor>()!;

    protected MoHttpApi(IMoServiceProvider provider, HttpClient httpClient) : base(provider)
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