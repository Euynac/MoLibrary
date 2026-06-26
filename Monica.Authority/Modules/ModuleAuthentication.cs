using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Monica.Authority.Authentication.Abstractions;
using Monica.Authority.Authentication.Services;
using Monica.Authority.Identity.Abstractions;
using Monica.Authority.Identity.Models;
using Monica.Authority.Identity.Services;
using Monica.Authority.Localization;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleAuthenticationBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the Authentication module
        /// </summary>
        public static ModuleAuthenticationGuide AddAuthentication(Action<ModuleAuthenticationOption>? action = null)
        {
            return new ModuleAuthenticationGuide().Register(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.Authentication)]
public class ModuleAuthentication(ModuleAuthenticationOption option) : WebModuleBase<ModuleAuthentication, ModuleAuthenticationOption, ModuleAuthenticationGuide>(option)
{
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<AuthorityResource>();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<AuthorityMessageLocalizer>();

        if (option.IsDebugging)
        {
            //https://aka.ms/IdentityModel/PII
            IdentityModelEventSource.ShowPII = true;
            IdentityModelEventSource.LogCompleteSecurityArtifact = true;
        }
        // Relies on AsyncLocal so the async static singleton keeps a separate HttpContext per request thread
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<IJwtAuthManager, JwtAuthManager>();
        services.AddSingleton<IAccessTokenIssuer, JwtAuthManager>();
        services.AddSingleton<ICurrentPrincipalAccessor, CurrentPrincipalAccessor>(); // Singleton is sufficient here

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

        #region Controller handling
        // Could also implement this through the Options.Event in AddJwtBearer?
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
                var jwt = context.RequestServices.GetRequiredService<IJwtAuthManager>();
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
            .WithName("DecodeJwtToken")
            .WithTags(tagName)
            .WithSummary("Decode a JWT token")
            .WithDescription("Decode a JWT token and return its claims and token metadata.");
        });
    }

    // Must be registered after the CORS middleware; otherwise CORS stops working
    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        app.UseAuthentication();
    }
}

public class ModuleAuthenticationGuide : WebModuleGuide<ModuleAuthentication, ModuleAuthenticationOption, ModuleAuthenticationGuide>
{
    private const string CONFIG_SYSTEM_USER = nameof(CONFIG_SYSTEM_USER);

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [CONFIG_SYSTEM_USER];
    }

    public ModuleAuthenticationGuide ConfigSystemUser<T>(T curSystemEnum, Action<SystemUserOptions>? action = null) where T : struct, Enum
    {
        return ConfigSystemUserCore(curSystemEnum, ModuleRegistrationOrder.Normal, action);
    }

    public ModuleAuthenticationGuide ConfigDefaultSystemUser(Action<SystemUserOptions>? action = null)
    {
        return ConfigSystemUserCore(EDefaultSystemUser.System, ModuleRegistrationOrder.PreConfig, action);
    }

    private ModuleAuthenticationGuide ConfigSystemUserCore<T>(
        T curSystemEnum,
        ModuleRegistrationOrder order,
        Action<SystemUserOptions>? action = null) where T : struct, Enum
    {
        ConfigureServices(context =>
        {
            context.Services.Configure((SystemUserOptions o) =>
            {
                o.SetCurSystemUser(curSystemEnum);
                action?.Invoke(o);
            });
            context.Services.AddSingleton<ISystemUserManager, SystemUserManager>();
        }, order,
            key: CONFIG_SYSTEM_USER,
            duplicateBehavior: ModuleConfigurationDuplicateBehavior.ExclusiveLastWins);
        return this;
    }
}

public class ModuleAuthenticationOption : MinimalApiModuleOptions<ModuleAuthentication>
{
    // Dangerous pitfall: the secret must exceed 128 bits, otherwise pad it to that length, and keep the same secret instead of generating it dynamically
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
