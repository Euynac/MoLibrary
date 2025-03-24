using System.Security.Claims;

namespace MoLibrary.Authority.Security;

public interface IMoCurrentPrincipalAccessor
{
    ClaimsPrincipal Principal { get; }
}
