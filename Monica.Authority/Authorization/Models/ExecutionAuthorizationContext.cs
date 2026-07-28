using System.Security.Claims;
using Monica.Core.Execution;

namespace Monica.Authority.Authorization.Models;

/// <summary>
/// Carries the immutable execution metadata and principal used for one authorization decision.
/// </summary>
/// <param name="Descriptor">The execution boundary being authorized.</param>
/// <param name="Principal">The current principal.</param>
public sealed record ExecutionAuthorizationContext(
    ExecutionDescriptor Descriptor,
    ClaimsPrincipal Principal);
