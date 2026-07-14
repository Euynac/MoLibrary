using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the Authentication module
        /// </summary>
        public ModuleAuthenticationGuide AddAuthentication(Action<ModuleAuthenticationOption>? action = null)
        {
            return builder.AddModule<ModuleAuthentication, ModuleAuthenticationOption, ModuleAuthenticationGuide>(action);
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
            x.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"].ToString();
                    var requestPath = context.Request.Path;
                    var isAllowedPath = option.QueryStringAccessTokenPathPrefixes.Any(prefix =>
                        requestPath.StartsWithSegments(
                            new PathString(prefix),
                            StringComparison.OrdinalIgnoreCase));

                    if (isAllowedPath && !string.IsNullOrEmpty(accessToken))
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

    /// <summary>
    /// Allows JWT bearer tokens to be read from the <c>access_token</c> query parameter only for the
    /// specified request-path prefixes.
    /// </summary>
    /// <remarks>
    /// Query-string tokens can be exposed by browser history, proxy logs, and server access logs. Use this
    /// only for transports such as browser WebSockets that cannot set an authorization header, and scope each
    /// prefix to a mapped hub route. Header-based bearer authentication remains enabled for every route.
    /// </remarks>
    /// <param name="pathPrefixes">One or more application-relative path prefixes, such as <c>/hubs/orders</c>.</param>
    /// <returns>The current authentication guide.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when a prefix is empty or would allow query-string tokens on the entire application.
    /// </exception>
    public ModuleAuthenticationGuide AllowQueryStringAccessTokens(params string[] pathPrefixes)
    {
        ArgumentNullException.ThrowIfNull(pathPrefixes);
        if (pathPrefixes.Length == 0)
        {
            throw new ArgumentException("At least one query-string access-token path prefix is required.", nameof(pathPrefixes));
        }

        var normalizedPrefixes = pathPrefixes
            .Select(NormalizeQueryStringTokenPathPrefix)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ConfigureModuleOption(option =>
        {
            foreach (var prefix in normalizedPrefixes)
            {
                if (!option.QueryStringAccessTokenPathPrefixes.Contains(prefix, StringComparer.OrdinalIgnoreCase))
                {
                    option.QueryStringAccessTokenPathPrefixes.Add(prefix);
                }
            }
        });

        return this;
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

    private static string NormalizeQueryStringTokenPathPrefix(string pathPrefix)
    {
        if (string.IsNullOrWhiteSpace(pathPrefix))
        {
            throw new ArgumentException("A query-string access-token path prefix cannot be empty.", nameof(pathPrefix));
        }

        var normalized = "/" + pathPrefix.Trim().Trim('/');
        if (normalized == "/")
        {
            throw new ArgumentException(
                "Query-string access tokens cannot be enabled for the entire application.",
                nameof(pathPrefix));
        }

        return normalized;
    }
}

/// <summary>
/// Configures JWT validation and token lifetimes for the current Monica host.
/// </summary>
public class ModuleAuthenticationOption : ModuleOptions<ModuleAuthentication>
{
    internal List<string> QueryStringAccessTokenPathPrefixes { get; } = [];

    /// <summary>
    /// Gets the signing key derived from <see cref="Secret"/>.
    /// </summary>
    public SymmetricSecurityKey SecurityKey => new(Encoding.ASCII.GetBytes(Secret.PadRight(512 / 8, '\0')));

    /// <summary>
    /// Gets or sets the stable secret used to sign access and refresh tokens.
    /// </summary>
    /// <remarks>
    /// The built-in value is a development placeholder. Production hosts must supply protected key material
    /// with at least 128 bits of entropy and preserve it for the intended token-validation lifetime.
    /// </remarks>
    public string Secret { get; set; } = nameof(Secret) + nameof(Secret);

    /// <summary>
    /// Gets or sets the issuer that generated and validates Monica JWTs.
    /// </summary>
    public string Issuer { get; set; } = nameof(Issuer);

    /// <summary>
    /// Gets or sets the audience accepted by Monica JWT validation.
    /// </summary>
    public string Audience { get; set; } = nameof(Audience);

    /// <summary>
    /// Gets or sets the access-token lifetime in minutes. The default is 60 minutes.
    /// </summary>
    public int AccessTokenExpiration { get; set; } = 60;

    /// <summary>
    /// Gets or sets the refresh-token lifetime in minutes. The default is 120 minutes.
    /// </summary>
    public int RefreshTokenExpiration { get; set; } = 120;
}
