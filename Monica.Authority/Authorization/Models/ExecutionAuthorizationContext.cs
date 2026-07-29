using System.Security.Claims;
using Monica.Core.Execution;

namespace Monica.Authority.Authorization.Models;

/// <summary>
/// Carries execution metadata, invocation state, and the principal used for one authorization decision.
/// </summary>
/// <param name="Descriptor">The execution boundary being authorized.</param>
/// <param name="Principal">The current principal.</param>
/// <param name="Input">The typed operation input, boxed for adapter-neutral authorization.</param>
/// <param name="Target">The concrete execution target when the adapter exposes one.</param>
/// <param name="Features">Adapter-specific invocation metadata.</param>
public sealed record ExecutionAuthorizationContext(
    ExecutionDescriptor Descriptor,
    ClaimsPrincipal Principal,
    object? Input,
    object? Target,
    ExecutionFeatureCollection Features)
{
    /// <summary>
    /// Gets the default adapter-neutral authorization resource for this invocation.
    /// </summary>
    /// <remarks>
    /// The authorization service uses the MVC action <c>HttpContext</c> for direct MVC inputs. Other adapters use this
    /// value, which prefers the concrete target and otherwise falls back to the operation input.
    /// </remarks>
    public object? DefaultResource => Target ?? Input;
}
