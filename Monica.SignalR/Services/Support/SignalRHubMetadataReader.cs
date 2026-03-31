using System.Reflection;
using Monica.Modules;
using Monica.SignalR.Models;
using SignalRSwaggerGen.Attributes;

namespace Monica.SignalR.Services.Support;

/// <summary>
/// Reads SignalR hub metadata from mapped hub registrations.
/// </summary>
internal sealed class SignalRHubMetadataReader
{
    /// <summary>
    /// Builds hub metadata for the supplied registrations.
    /// </summary>
    public List<SignalRHubInfo> ReadHubMetadata(IEnumerable<SignalRHubRegistration> hubRegistrations)
    {
        return hubRegistrations
            .Select(hubRegistration => new SignalRHubInfo
            {
                HubName = hubRegistration.HubType.Name,
                Route = hubRegistration.HubRoute,
                Methods = hubRegistration.HubType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Select(methodInfo => new SignalRHubMethodInfo
                    {
                        Description = methodInfo.GetCustomAttribute<SignalRMethodAttribute>()?.Description ?? methodInfo.Name,
                        Name = methodInfo.Name,
                        Parameters = methodInfo.GetParameters()
                            .Select(parameterInfo => new SignalRHubParameterInfo
                            {
                                Name = parameterInfo.Name ?? string.Empty,
                                TypeName = parameterInfo.ParameterType.Name
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();
    }
}
