using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Monica.Authority.Authentication;
using Monica.Authority.Implements.Security;
using Monica.Authority.Security;
using Monica.Core;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleAuthenticationBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 Authentication 模块
        /// </summary>
        public static ModuleAuthenticationGuide AddAuthentication(Action<ModuleAuthenticationOption>? action = null)
        {
            return new ModuleAuthenticationGuide().Register(action);
        }
    }
}

public class ModuleAuthentication(ModuleAuthenticationOption option) : MoModule<ModuleAuthentication, ModuleAuthenticationOption, ModuleAuthenticationGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Authentication;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        if (option.IsDebugging)
        {
            //https://aka.ms/IdentityModel/PII
            IdentityModelEventSource.ShowPII = true;
            IdentityModelEventSource.LogCompleteSecurityArtifact = true;
        }
        //依赖于AsyncLocal技术，异步static单例，不同的请求线程会有不同的HttpContext
        services.AddHttpContextAccessor();
        services.AddScoped<IMoCurrentUser, MoCurrentUser>();
        services.AddSingleton<IMoJwtAuthManager, MoJwtAuthManager>();
        services.AddSingleton<IMoAuthManager, MoJwtAuthManager>();
        services.AddSingleton<IMoCurrentPrincipalAccessor, MoCurrentPrincipalAccessor>(); //为何用单例就行？


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
                ValidIssuer = option.Issuer,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = option.SecurityKey,
                ValidAudience = option.Audience,
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

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();

            endpoints.MapGet("/jwt/decode/{token}", async (HttpResponse response, HttpContext context, string token) =>
            {
                var jwt = context.RequestServices.GetRequiredService<IMoJwtAuthManager>();
                var (claims, tokenInfo) = jwt.DecodeJwtToken(token);
                await context.Response.WriteAsJsonAsync(new
                {
                    claims = claims.Claims.Select(p => new
                    {
                        p.Type,
                        p.Value
                    }),
                    tokenInfo
                });
            })
            .WithName("JWT解码")
            .WithTags(tagName)
            .WithSummary("JWT解码")
            .WithDescription("JWT解码");
        });
    }

    //必须在CORS中间件之后，不然会使得CORS失效
    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        app.UseAuthentication();
    }
}

public class ModuleAuthenticationGuide : MoModuleGuide<ModuleAuthentication, ModuleAuthenticationOption, ModuleAuthenticationGuide>
{
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(ConfigSystemUser)];
    }
    public ModuleAuthenticationGuide ConfigSystemUser<T>(T curSystemEnum, Action<MoSystemUserOptions>? action = null) where T : struct, Enum
    {
        ConfigureServices(context =>
        {
            context.Services.Configure((MoSystemUserOptions o) =>
            {
                o.SetCurSystemUser(curSystemEnum);
                action?.Invoke(o);
            });
            context.Services.AddSingleton<IMoSystemUserManager, MoSystemUserManager>();
        });
        return this;
        
    }

    public ModuleAuthenticationGuide ConfigDefaultSystemUser(Action<MoSystemUserOptions>? action = null)
    {
        return ConfigSystemUser(EMoDefaultSystemUser.System, action);
    }
}

public class ModuleAuthenticationOption : MoModuleOptionWithMinimalApi<ModuleAuthentication>
{
    //巨坑：Secret的长度必须大于128bit，否则需要补全到该长度。而且Secret必须一致，不可动态生成
    public SymmetricSecurityKey SecurityKey => new(Encoding.ASCII.GetBytes(Secret.PadRight(512 / 8, '\0')));
    public string Secret { get; set; } = nameof(Secret) + nameof(Secret);
    /// <summary>
    /// Can not be null or empty if validate Issuer.
    /// </summary>
    public string Issuer { get; set; } = nameof(Issuer);

    /// <summary>
    /// Can not be null or empty if validate audience.
    /// </summary>
    public string Audience { get; set; } = nameof(Audience);

    /// <summary>
    /// Access Token expiration time. Unit: Minutes
    /// </summary>
    public int AccessTokenExpiration { get; set; } = 60;

    /// <summary>
    /// Refresh Token expiration time. Unit: Minutes
    /// </summary>
    public int RefreshTokenExpiration { get; set; } = 120;
    public bool IsDebugging { get; set; }
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
