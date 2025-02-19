using System.Security.Claims;

namespace BuildingBlocksPlatform.Authority.Security;

public interface IMoCurrentPrincipalAccessor
{
    ClaimsPrincipal Principal { get; }
}
