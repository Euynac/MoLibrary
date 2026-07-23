// ReSharper disable once CheckNamespace
namespace Monica.Core.Results;

/// <summary>
/// Defines the transport-neutral status carried by a Monica result envelope.
/// </summary>
public enum ResStatus
{
    /// <summary>
    /// No result status has been assigned.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The request completed successfully.
    /// </summary>
    Ok = 200,

    /// <summary>
    /// The request completed and created a resource.
    /// </summary>
    Created = 201,

    /// <summary>
    /// The request is invalid.
    /// </summary>
    BadRequest = 400,

    /// <summary>
    /// Authentication is required.
    /// </summary>
    Unauthorized = 401,

    /// <summary>
    /// The authenticated caller is not allowed to perform the operation.
    /// </summary>
    Forbidden = 403,

    /// <summary>
    /// The requested resource does not exist.
    /// </summary>
    NotFound = 404,

    /// <summary>
    /// The request payload exceeds the server's accepted size.
    /// </summary>
    PayloadTooLarge = 413,

    /// <summary>
    /// The request content type is not supported by the target operation.
    /// </summary>
    UnsupportedMediaType = 415,

    /// <summary>
    /// Request validation failed.
    /// </summary>
    ValidateError = 451,

    /// <summary>
    /// The refresh token has expired or is invalid.
    /// </summary>
    RefreshTokenExpired = 452,

    /// <summary>
    /// The access token has expired or is invalid.
    /// </summary>
    AccessTokenExpired = 453,

    /// <summary>
    /// The operation requires explicit user confirmation before it can continue.
    /// </summary>
    ErrorWarning = 460,

    /// <summary>
    /// An unexpected server error occurred.
    /// </summary>
    InternalError = 500,
}
