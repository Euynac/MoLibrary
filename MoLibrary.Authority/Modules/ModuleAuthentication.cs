using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MoLibrary.Authority.Authentication;
using MoLibrary.Authority.Implements.Security;
using MoLibrary.Authority.Security;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Authority.Modules;

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
public class ModuleAuthentication(ModuleAuthenticationOption option) : MoModule<ModuleAuthentication, ModuleAuthenticationOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Authentication;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {
        if (option.IsDebugging)
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

        return Res.Ok();
    }

    public override Res ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = option.GetSwaggerGroupName(), Description = "JWT相关接口" }
            };
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

            }).WithName("JWT解码").WithOpenApi(operation =>
            {
                operation.Summary = "JWT解码";
                operation.Description = "JWT解码";
                operation.Tags = tagGroup;
                return operation;
            });
        });
        return base.ConfigureEndpoints(app);
    }

    //必须在CORS中间件之后，不然会使得CORS失效
    public override Res ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        app.UseAuthentication();
        return Res.Ok();
    }
}