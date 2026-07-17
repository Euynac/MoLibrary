using System.Security.Claims;
using Monica.Authority.Identity.Abstractions;

namespace Monica.Testing.Doubles;

/// <summary>
/// Deterministic current-user implementation for application and repository tests.
/// </summary>
public sealed class TestCurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal _claimsPrincipal;

    /// <summary>
    /// Initializes an anonymous test user.
    /// </summary>
    public TestCurrentUser()
        : this(id: null, username: null)
    {
    }

    /// <summary>
    /// Initializes a test user with explicit identity values.
    /// </summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="username">The username.</param>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="nickname">The display nickname.</param>
    /// <param name="claims">Additional claims attached to the principal.</param>
    public TestCurrentUser(
        string? id,
        string? username,
        string? roleId = null,
        string? nickname = null,
        IEnumerable<Claim>? claims = null)
    {
        Id = id;
        Username = username;
        RoleId = roleId;
        Nickname = nickname;

        var identity = new ClaimsIdentity(claims ?? [], id is null ? null : "Test");
        _claimsPrincipal = new ClaimsPrincipal(identity);
    }

    /// <inheritdoc />
    public bool IsAuthenticated => Id is not null;

    /// <inheritdoc />
    public ClaimsPrincipal ClaimsPrincipal => _claimsPrincipal;

    /// <inheritdoc />
    public string? Id { get; }

    /// <inheritdoc />
    public string? RoleId { get; }

    /// <inheritdoc />
    public string? Nickname { get; }

    /// <inheritdoc />
    public string? Username { get; }

    /// <inheritdoc />
    public Claim? FindClaim(string claimType)
    {
        return ClaimsPrincipal.FindFirst(claimType);
    }

    /// <inheritdoc />
    public Claim[] FindClaims(string claimType)
    {
        return ClaimsPrincipal.FindAll(claimType).ToArray();
    }

    /// <inheritdoc />
    public Claim[] GetAllClaims()
    {
        return ClaimsPrincipal.Claims.ToArray();
    }

    /// <inheritdoc />
    public string? FindClaimValue(string claimType)
    {
        return FindClaim(claimType)?.Value;
    }

    /// <inheritdoc />
    public T FindClaimValue<T>(string claimType)
        where T : struct
    {
        var value = FindClaimValue(claimType);
        return value is null
            ? default
            : (T)Convert.ChangeType(value, typeof(T));
    }
}
