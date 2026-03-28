namespace Monica.Core.Results;

/// <summary>
/// Universal return code
/// </summary>
public enum ResStatus
{
    Unknown = 0,
    /// <summary>
    /// Request normal
    /// </summary>
    Ok = 200,
    /// <summary>
    /// Request + create new resource
    /// </summary>
    Created = 201,
    /// <summary>
    /// Request error
    /// </summary>
    BadRequest = 400,
    /// <summary>
    /// Not logged in
    /// </summary>
    Unauthorized = 401,
    /// <summary>
    /// Refresh Token invalid
    /// </summary>
    RefreshTokenExpired = 452,
    /// <summary>
    /// Access Token is invalid
    /// </summary>
    AccessTokenExpired = 453,

    /// <summary>
    /// Warning error, generally requires user confirmation
    /// </summary>
    ErrorWarning = 460,
    /// <summary>
    /// Insufficient permissions
    /// </summary>
    Forbidden = 403,
    /// <summary>
    /// Input validation error
    /// </summary>
    ValidateError = 451,
    /// <summary>
    /// System exception
    /// </summary>
    InternalError = 500,
}
