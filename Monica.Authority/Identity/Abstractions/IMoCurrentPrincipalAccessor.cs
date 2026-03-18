using System.Security.Claims;

namespace Monica.Authority.Identity.Abstractions;

public interface IMoCurrentPrincipalAccessor
{
    ClaimsPrincipal Principal { get; }
}
