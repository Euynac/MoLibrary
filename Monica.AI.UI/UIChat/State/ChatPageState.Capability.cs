using Monica.AI.AgentCapabilities.Models;
using Monica.Core.Results;
using MudBlazor;

namespace Monica.AI.UI.UIChat.State;

public sealed partial class ChatPageState
{
    private async Task LoadCapabilityCandidatesAsync()
    {
        var result = await _capabilityFacade.GetReferenceCandidatesAsync();
        if (result.IsFailed(out var error, out var candidates))
        {
            CapabilityCandidates = [];
            _snackbar.Add(
                string.IsNullOrWhiteSpace(error.Message)
                    ? _localizer["Error:Generic"]
                    : error.Message,
                Severity.Warning);
            return;
        }

        CapabilityCandidates = candidates
            .Where(static candidate => candidate.IsEnabled)
            .ToList();
    }
}
