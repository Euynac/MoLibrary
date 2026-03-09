using Microsoft.AspNetCore.Components;
using Monica.UI.UICore.Models;

namespace Monica.UI.Components.Layout;

public partial class MoNavBarMore : IDisposable
{
    private const int CloseDelayMilliseconds = 120;

    [Parameter] public Dictionary<string, List<UINavItem>> Categories { get; set; } = new();
    [Parameter] public bool CompactMode { get; set; }

    private bool _isDropdownOpen;
    private bool _isPointerInsideMenu;
    private long _closeRequestVersion;
    private string? _activeFlyoutCategory;
    private CancellationTokenSource? _closeDelayCts;
    private string RootCssClass => CompactMode ? "navbar-more navbar-more-compact" : "navbar-more";
    private string MoreMenuCssClass => _activeFlyoutCategory is null
        ? "dropdown-menu more-menu more-menu-scrollable"
        : "dropdown-menu more-menu";

    private string GetItemText(UINavItem item)
    {
        if (!string.IsNullOrEmpty(item.TextKey))
        {
            return RegistryL[item.TextKey];
        }

        return item.Text;
    }

    private void OnMouseEnter()
    {
        _isPointerInsideMenu = true;
        CancelPendingClose();
        SetDropdownOpen(isOpen: true);
    }

    private void OnMouseLeave()
    {
        _isPointerInsideMenu = false;
        ScheduleDelayedClose();
    }

    private void OnCategoryHover(string category)
    {
        _isPointerInsideMenu = true;
        CancelPendingClose();

        if (_activeFlyoutCategory == category)
        {
            return;
        }

        _activeFlyoutCategory = category;
        StateHasChanged();
    }

    private void OnCategoryLeave()
    {
        if (!_isPointerInsideMenu)
        {
            ScheduleDelayedClose();
        }
    }

    private void SetDropdownOpen(bool isOpen)
    {
        if (_isDropdownOpen == isOpen)
        {
            return;
        }

        _isDropdownOpen = isOpen;

        if (!isOpen)
        {
            _activeFlyoutCategory = null;
        }

        StateHasChanged();
    }

    private void ScheduleDelayedClose()
    {
        CancelPendingClose();
        _closeDelayCts = new CancellationTokenSource();
        var closeRequestVersion = ++_closeRequestVersion;
        _ = CloseAfterDelayAsync(closeRequestVersion, _closeDelayCts.Token);
    }

    private async Task CloseAfterDelayAsync(long closeRequestVersion, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(CloseDelayMilliseconds, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            await InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (closeRequestVersion != _closeRequestVersion || _isPointerInsideMenu)
                {
                    return;
                }

                _isDropdownOpen = false;
                _activeFlyoutCategory = null;
                StateHasChanged();
            });
        }
        catch (OperationCanceledException)
        {
            // Ignored: cancellation is expected during hover transitions.
        }
        catch (ObjectDisposedException)
        {
            // Ignored: component disposed while delayed close task was pending.
        }
        catch (InvalidOperationException)
        {
            // Ignored: renderer can be unavailable during teardown.
        }
        catch (Exception)
        {
            // Ignored: avoid unobserved fire-and-forget exceptions.
        }
    }

    private void CancelPendingClose()
    {
        if (_closeDelayCts is null)
        {
            return;
        }

        _closeDelayCts.Cancel();
        _closeDelayCts.Dispose();
        _closeDelayCts = null;
    }

    public void Dispose()
    {
        CancelPendingClose();
    }
}
