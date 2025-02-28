using BuildingBlocksPlatform.Authority.Authentication;
using BuildingBlocksPlatform.Authority.Implements.Security;
using BuildingBlocksPlatform.Authority.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocksPlatform.Authority;

public static class MoJwtBuilderExtensions
{
    public static void AddMoSystemUser<T>(this IServiceCollection services, T curSystemEnum, Action<MoSystemUserOptions>? action = null) where T : struct, Enum
    {
        services.Configure((MoSystemUserOptions o) =>
        {
            o.SetCurSystemUser(curSystemEnum);
            action?.Invoke(o);
        });
    }

    /// <summary>
    /// 注册JWT服务
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configAction"></param>
    public static void AddMoAuthenticationJwt(this IServiceCollection services, Action<MoJwtTokenOptions>? configAction = null)
    {
        var config = new MoJwtTokenOptions();
        if (configAction != null)
        {
            services.Configure(configAction);
            configAction.Invoke(config);
        }

        if (config.IsDebugging)
        {
            //https://aka.ms/IdentityModel/PII
            IdentityModelEventSource.ShowPII = true;
            IdentityModelEventSource.LogCompleteSecurityArtifact = true;
        }
        //依赖于AsyncLocal技术，异步static单例，不同的请求线程会有不同的HttpContext
        services.AddHttpContextAccessor();
        services.AddTransient<IMoCurrentUser, MoCurrentUser>();
        services.AddSingleton<IMoJwtAuthManager, MoJwtAuthManager>();
        services.AddSingleton<IMoAuthManager, MoJwtAuthManager>();
        services.AddSingleton<IMoCurrentPrincipalAccessor, MoCurrentPrincipalAccessor>(); //为何用单例就行？

        #region SystemUser
        
        services.AddSingleton<IMoSystemUserManager, MoSystemUserManager>();
        services.AddMoSystemUser(EMoDefaultSystemUser.System);
        

        #endregion
        
        services.AddSingleton<IPasswordCrypto, PasswordCrypto>();
        services.AddAuthentication(x =>
        {
            x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(x =>
        {
            x.RequireHttpsMetadata = true;
            x.SaveToken = true;
            x.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = config.Issuer,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = config.SecurityKey,
                ValidAudience = config.Audience,
                ValidateAudience = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
            // We have to hook the OnMessageReceived event in order to allow the JWT authentication handler 
            // to read the access token from the query string when a WebSocket or Server-Sent Events request comes in.
            // SignalR is unable to set headers in browsers when using some transports.
            x.Events = new JwtBearerEvents()
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    //var path = context.HttpContext.Request.Path;
                    //path.StartsWithSegments("/signalr");
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });


        #region Controller处理
        //还可以通过AddJwtBearer中的Options中的Event实现？
        //services.AddControllers(o =>
        //{
        //    o.Filters.Add(new CustomAuthorizeFilter());
        //});

        #endregion

    }
  
    /// <summary>
    /// 使用JWT中间件，必须在CORS中间件之后，不然会使得CORS失效
    /// </summary>
    /// <param name="app"></param>
    public static void UseMoAuthenticationJwt(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }
}

public class CustomAuthorizeFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity == null)
        {
            return;
        }

        if (!context.HttpContext.User.Identity.IsAuthenticated)
        {
            return;
        }
    }
}