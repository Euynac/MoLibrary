using System.Security.Claims;

namespace Monica.Authority.Identity.Abstractions;

public interface ICurrentPrincipalAccessor
{
    ClaimsPrincipal Principal { get; }
}
