namespace Monica.UI.Services;

/// <summary>
/// Monica user context service - provides current user information
/// </summary>
public class MoUserContextService
{
    /// <summary>
    /// Username
    /// </summary>
    public string UserName => "Mo User";

    /// <summary>
    /// user role
    /// </summary>
    public string UserRole => "Administrator";

    /// <summary>
    /// Is online
    /// </summary>
    public bool IsOnline => true;

    /// <summary>
    /// User abbreviation (for avatar display)
    /// </summary>
    public string UserInitials => "MO";

    // TODO: In the future, this can be expanded to obtain real user information from IHttpContextAccessor
    // or the authentication system.
}
