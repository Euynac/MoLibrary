using System.Security.Claims;

namespace Monica.Authority.Security;

public interface IMoCurrentPrincipalAccessor
{
    ClaimsPrincipal Principal { get; }
}
