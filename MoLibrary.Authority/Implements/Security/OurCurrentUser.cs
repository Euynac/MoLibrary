using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Authority.Security;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Authority.Implements.Security;

public class OurCurrentUser(ClaimsPrincipal principal)
    : MoCurrentUser(principal), IOurCurrentUser
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    [ActivatorUtilitiesConstructor]
    public OurCurrentUser(IMoCurrentPrincipalAccessor principalAccessor, IHttpContextAccessor httpContextAccessor):this(principalAccessor.Principal)
    {
        _httpContextAccessor = httpContextAccessor;
    }
   

    public string? DefaultBit => FindClaimValue(OurClaimTypes.DefaultBit);

    public virtual string? IpAddress => _httpContextAccessor?.HttpContext is { } context
        ? context.Request.Headers["X-Real-IP"].ToString().BeNullIfWhiteSpace() ?? context.Connection.RemoteIpAddress?.ToString() : null;
}

