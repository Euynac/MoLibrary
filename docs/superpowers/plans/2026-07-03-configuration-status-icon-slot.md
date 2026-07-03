# Configuration Status Icon Slot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace fragile `MudBadge` title-row status badges with an owned Configuration UI status icon slot that keeps the count attached to the icon without clipping.

**Architecture:** Add a small reusable `ConfigurationStatusIconButton` component under `Monica.Configuration.UI/Components`. The component owns icon/count geometry and exposes simple rendering inputs; existing parent components keep all status data, popover, reload, and validation behavior.

**Tech Stack:** Blazor, MudBlazor v9, CSS isolation, Monica.Configuration.UI localization resources already used by parent components.

---

## File Structure

- Create `Monica.Configuration.UI/Components/ConfigurationStatusIconButton.razor`
  - Owns the status icon slot, `MudIconButton`, optional count badge, count capping, and click/accessibility parameters.
- Create `Monica.Configuration.UI/Components/ConfigurationStatusIconButton.razor.css`
  - Owns the fixed slot geometry and count badge styling.
- Modify `Monica.Configuration.UI/Pages/ConfigurationStatePage.razor`
  - Replace the runtime validation `MudBadge` markup with `ConfigurationStatusIconButton`.
- Modify `Monica.Configuration.UI/Pages/ConfigurationStatePage.razor.css`
  - Remove runtime validation `MudBadge` internal-class overrides.
  - Keep only anchor styling needed for wrapper/popover placement.
- Modify `Monica.Configuration.UI/Components/ConfigurationReloadStatusButton.razor`
  - Replace the reload `MudBadge` markup with `ConfigurationStatusIconButton`.
- Modify `Monica.Configuration.UI/Components/ConfigurationReloadStatusButton.razor.css`
  - Remove reload `MudBadge` internal-class overrides.
  - Keep only anchor styling needed for wrapper/popover placement.

No new localization keys are required because the new component receives existing localized tooltip and aria-label strings from parents.

## Task 1: Add The Owned Status Icon Component

**Files:**
- Create: `Monica.Configuration.UI/Components/ConfigurationStatusIconButton.razor`
- Create: `Monica.Configuration.UI/Components/ConfigurationStatusIconButton.razor.css`

- [ ] **Step 1: Create the Razor component**

Create `Monica.Configuration.UI/Components/ConfigurationStatusIconButton.razor` with this content:

```razor
@using System.Globalization

<MudTooltip Text="@Tooltip">
    <span class="configuration-status-icon-slot">
        <MudIconButton Icon="@Icon"
                       Color="@Color"
                       Size="Size.Small"
                       Disabled="@Disabled"
                       OnClick="@OnClick"
                       aria-label="@AriaLabel" />
        @if (BadgeVisible)
        {
            <span class="configuration-status-icon-badge" aria-hidden="true">@BadgeText</span>
        }
    </span>
</MudTooltip>

@code {
    private const int MAX_BADGE_COUNT = 99;

    [Parameter, EditorRequired]
    public string Icon { get; set; } = string.Empty;

    [Parameter]
    public Color Color { get; set; } = Color.Default;

    [Parameter]
    public int Count { get; set; }

    [Parameter]
    public bool ShowCount { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string? Tooltip { get; set; }

    [Parameter, EditorRequired]
    public string AriaLabel { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    private bool BadgeVisible => ShowCount && Count > 0;

    private string BadgeText => Count > MAX_BADGE_COUNT
        ? $"{MAX_BADGE_COUNT.ToString(CultureInfo.InvariantCulture)}+"
        : Count.ToString(CultureInfo.InvariantCulture);
}
```

- [ ] **Step 2: Create the component CSS**

Create `Monica.Configuration.UI/Components/ConfigurationStatusIconButton.razor.css` with this content:

```css
.configuration-status-icon-slot {
    position: relative;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex: 0 0 auto;
    width: 2.5rem;
    min-width: 2.5rem;
    height: 2.25rem;
    overflow: visible;
    line-height: 1;
}

.configuration-status-icon-badge {
    position: absolute;
    top: 0.125rem;
    right: 0.125rem;
    z-index: 1;
    box-sizing: border-box;
    min-width: 1.25rem;
    height: 1.25rem;
    padding: 0 0.375rem;
    border-radius: 999px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    color: var(--mud-palette-error-text);
    background: var(--mud-palette-error);
    font-size: 0.75rem;
    font-weight: 600;
    line-height: 1;
    letter-spacing: 0;
    pointer-events: none;
    white-space: nowrap;
}
```

- [ ] **Step 3: Build the UI project**

Run:

```bash
dotnet build 'D:\Code\MoLibrary\Monica.Configuration.UI\Monica.Configuration.UI.csproj' -m
```

Expected: build succeeds with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 4: Commit Task 1**

Run:

```bash
git add Monica.Configuration.UI/Components/ConfigurationStatusIconButton.razor Monica.Configuration.UI/Components/ConfigurationStatusIconButton.razor.css
git diff --cached --check
git commit -m "feat: add configuration status icon button"
```

Expected: staged whitespace check passes and commit succeeds.

## Task 2: Replace Runtime Validation Badge Usage

**Files:**
- Modify: `Monica.Configuration.UI/Pages/ConfigurationStatePage.razor`
- Modify: `Monica.Configuration.UI/Pages/ConfigurationStatePage.razor.css`

- [ ] **Step 1: Replace the runtime validation badge markup**

In `Monica.Configuration.UI/Pages/ConfigurationStatePage.razor`, replace the current runtime validation block inside `<span class="configuration-runtime-validation-anchor">`:

```razor
<MudTooltip Text="@RuntimeValidationStatusTooltip">
    <MudBadge Content="@RuntimeValidationIssueCount"
              Color="@RuntimeValidationStatusColor"
              Visible="@(RuntimeValidationIssueCount > 0)"
              Origin="Origin.TopRight"
              Overlap="true">
        <MudIconButton Icon="@RuntimeValidationStatusIcon"
                       Color="@RuntimeValidationStatusColor"
                       Size="Size.Small"
                       OnClick="ToggleRuntimeValidationPopover"
                       aria-label="@L["State:RuntimeValidation:OpenDetails"]" />
    </MudBadge>
</MudTooltip>
```

with:

```razor
<ConfigurationStatusIconButton Icon="@RuntimeValidationStatusIcon"
                               Color="@RuntimeValidationStatusColor"
                               Count="@RuntimeValidationIssueCount"
                               ShowCount="@(RuntimeValidationIssueCount > 0)"
                               Tooltip="@RuntimeValidationStatusTooltip"
                               AriaLabel="@L["State:RuntimeValidation:OpenDetails"].Value"
                               OnClick="ToggleRuntimeValidationPopover" />
```

Leave the surrounding `<span class="configuration-runtime-validation-anchor">`, `MudPopover`, and `MudOverlay` unchanged.

- [ ] **Step 2: Simplify the runtime validation anchor CSS**

In `Monica.Configuration.UI/Pages/ConfigurationStatePage.razor.css`, replace this block:

```css
.configuration-runtime-validation-anchor {
    position: relative;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex: 0 0 auto;
    width: 2rem;
    height: 2rem;
    overflow: visible;
    line-height: 1;
}

.configuration-runtime-validation-anchor ::deep .mud-tooltip-root,
.configuration-runtime-validation-anchor ::deep .mud-badge-root {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    overflow: visible;
}

.configuration-runtime-validation-anchor ::deep .mud-badge-wrapper {
    overflow: visible;
}

.configuration-runtime-validation-anchor ::deep .mud-badge-wrapper.mud-badge-top.right,
.configuration-runtime-validation-anchor ::deep .mud-badge-wrapper.mud-badge-center.right {
    align-items: flex-start;
    justify-content: flex-end;
}

.configuration-runtime-validation-anchor ::deep .mud-badge.mud-badge-top.right,
.configuration-runtime-validation-anchor ::deep .mud-badge.mud-badge-center.right {
    inset: auto auto calc(100% - 16px) calc(100% - 12px);
}
```

with:

```css
.configuration-runtime-validation-anchor {
    position: relative;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex: 0 0 auto;
    overflow: visible;
    line-height: 1;
}
```

- [ ] **Step 3: Build the UI project**

Run:

```bash
dotnet build 'D:\Code\MoLibrary\Monica.Configuration.UI\Monica.Configuration.UI.csproj' -m
```

Expected: build succeeds with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 4: Commit Task 2**

Run:

```bash
git add Monica.Configuration.UI/Pages/ConfigurationStatePage.razor Monica.Configuration.UI/Pages/ConfigurationStatePage.razor.css
git diff --cached --check
git commit -m "fix: use owned status slot for validation badge"
```

Expected: staged whitespace check passes and commit succeeds.

## Task 3: Replace Reload Status Badge Usage

**Files:**
- Modify: `Monica.Configuration.UI/Components/ConfigurationReloadStatusButton.razor`
- Modify: `Monica.Configuration.UI/Components/ConfigurationReloadStatusButton.razor.css`

- [ ] **Step 1: Replace the reload badge markup**

In `Monica.Configuration.UI/Components/ConfigurationReloadStatusButton.razor`, replace this block:

```razor
<MudTooltip Text="@StatusTooltip">
    <MudBadge Content="@DriftCount"
              Color="@StatusColor"
              Visible="@(DriftCount > 0)"
              Origin="Origin.TopRight"
              Overlap="true">
        <MudIconButton Icon="@StatusIcon"
                       Color="@StatusColor"
                       Size="Size.Small"
                       Disabled="@_loading"
                       OnClick="TogglePopover"
                       aria-label="@L["State:ReloadStatus:OpenDetails"]" />
    </MudBadge>
</MudTooltip>
```

with:

```razor
<ConfigurationStatusIconButton Icon="@StatusIcon"
                               Color="@StatusColor"
                               Count="@DriftCount"
                               ShowCount="@(DriftCount > 0)"
                               Disabled="@_loading"
                               Tooltip="@StatusTooltip"
                               AriaLabel="@L["State:ReloadStatus:OpenDetails"].Value"
                               OnClick="TogglePopover" />
```

Leave the surrounding `<span class="configuration-reload-status-anchor">` and `MudPopover` unchanged.

- [ ] **Step 2: Simplify the reload anchor CSS**

In `Monica.Configuration.UI/Components/ConfigurationReloadStatusButton.razor.css`, replace this block:

```css
.configuration-reload-status-anchor {
    position: relative;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex: 0 0 auto;
    width: 2rem;
    height: 2rem;
    overflow: visible;
    line-height: 1;
}

.configuration-reload-status-anchor ::deep .mud-tooltip-root,
.configuration-reload-status-anchor ::deep .mud-badge-root {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    overflow: visible;
}

.configuration-reload-status-anchor ::deep .mud-badge-wrapper {
    overflow: visible;
}

.configuration-reload-status-anchor ::deep .mud-badge-wrapper.mud-badge-top.right,
.configuration-reload-status-anchor ::deep .mud-badge-wrapper.mud-badge-center.right {
    align-items: flex-start;
    justify-content: flex-end;
}

.configuration-reload-status-anchor ::deep .mud-badge.mud-badge-top.right,
.configuration-reload-status-anchor ::deep .mud-badge.mud-badge-center.right {
    inset: auto auto calc(100% - 16px) calc(100% - 12px);
}
```

with:

```css
.configuration-reload-status-anchor {
    position: relative;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex: 0 0 auto;
    overflow: visible;
    line-height: 1;
}
```

- [ ] **Step 3: Build the UI project**

Run:

```bash
dotnet build 'D:\Code\MoLibrary\Monica.Configuration.UI\Monica.Configuration.UI.csproj' -m
```

Expected: build succeeds with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 4: Commit Task 3**

Run:

```bash
git add Monica.Configuration.UI/Components/ConfigurationReloadStatusButton.razor Monica.Configuration.UI/Components/ConfigurationReloadStatusButton.razor.css
git diff --cached --check
git commit -m "fix: use owned status slot for reload badge"
```

Expected: staged whitespace check passes and commit succeeds.

## Task 4: Verify Browser Layout And Final Build

**Files:**
- Inspect: `Monica.Configuration.UI/Components/ConfigurationStatusIconButton.razor`
- Inspect: `Monica.Configuration.UI/Components/ConfigurationStatusIconButton.razor.css`
- Inspect: `Monica.Configuration.UI/Pages/ConfigurationStatePage.razor`
- Inspect: `Monica.Configuration.UI/Components/ConfigurationReloadStatusButton.razor`

- [ ] **Step 1: Verify no MudBadge internals remain in the title-row status implementation**

Run:

```bash
rg -n "mud-badge|MudBadge|mud-badge-wrapper|mud-badge-center|mud-badge-top" Monica.Configuration.UI/Pages/ConfigurationStatePage.razor Monica.Configuration.UI/Pages/ConfigurationStatePage.razor.css Monica.Configuration.UI/Components/ConfigurationReloadStatusButton.razor Monica.Configuration.UI/Components/ConfigurationReloadStatusButton.razor.css Monica.Configuration.UI/Components/ConfigurationStatusIconButton.razor Monica.Configuration.UI/Components/ConfigurationStatusIconButton.razor.css || true
```

Expected: no matches.

- [ ] **Step 2: Run focused UI build**

Run:

```bash
dotnet build 'D:\Code\MoLibrary\Monica.Configuration.UI\Monica.Configuration.UI.csproj' -m
```

Expected: build succeeds with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 3: Run full solution build**

Run:

```bash
dotnet build 'D:\Code\MoLibrary\Monica.slnx' -m
```

Expected: build succeeds with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 4: Run CSS token validation for changed CSS**

Run this scoped validation command from the repository root:

```bash
python - <<'PY'
import importlib.util
from pathlib import Path

script = Path('scripts/validate_ui_theme_tokens.py').resolve()
spec = importlib.util.spec_from_file_location('validate_ui_theme_tokens', script)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

paths = [
    Path('Monica.Configuration.UI/Components/ConfigurationStatusIconButton.razor.css').resolve(),
    Path('Monica.Configuration.UI/Pages/ConfigurationStatePage.razor.css').resolve(),
    Path('Monica.Configuration.UI/Components/ConfigurationReloadStatusButton.razor.css').resolve(),
]

issues = []
for path in paths:
    issues.extend(module.scan_file(path))

if issues:
    print('\n'.join(issues))
    raise SystemExit(1)

print('Scoped UI theme token validation passed for changed CSS files.')
PY
```

Expected: `Scoped UI theme token validation passed for changed CSS files.`

- [ ] **Step 5: Verify browser geometry at the live app**

Run this Playwright script from `.tmp/browser-verify`:

```bash
node <<'NODE'
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1010, height: 1114 }, deviceScaleFactor: 1 });
  await page.goto('http://localhost:5028/configuration/state?statusSlotCheck=' + Date.now(), {
    waitUntil: 'domcontentloaded',
    timeout: 30000
  });
  await page.waitForTimeout(3000);
  await page.screenshot({ path: '/tmp/config-status-slot-check.png', fullPage: false });

  const result = await page.evaluate(() => {
    const box = el => {
      if (!el) return null;
      const r = el.getBoundingClientRect();
      const cs = getComputedStyle(el);
      return {
        className: String(el.className),
        text: el.textContent?.trim(),
        x: Math.round(r.x * 10) / 10,
        y: Math.round(r.y * 10) / 10,
        right: Math.round(r.right * 10) / 10,
        bottom: Math.round(r.bottom * 10) / 10,
        width: Math.round(r.width * 10) / 10,
        height: Math.round(r.height * 10) / 10,
        overflow: cs.overflow
      };
    };

    return {
      titleRow: box(document.querySelector('.configuration-state-title-row')),
      slots: [...document.querySelectorAll('.configuration-status-icon-slot')].map(box),
      badges: [...document.querySelectorAll('.configuration-status-icon-badge')].map(box),
      mudBadgesInTitle: document.querySelectorAll('.configuration-state-title-row .mud-badge').length
    };
  });

  console.log(JSON.stringify(result, null, 2));
  await browser.close();
})().catch(error => {
  console.error(error);
  process.exit(1);
});
NODE
```

Expected:

- `mudBadgesInTitle` is `0`.
- At least one `.configuration-status-icon-slot` is present when status icons are rendered.
- Any `.configuration-status-icon-badge` top and bottom coordinates are inside or visually aligned with the slot, not above the page title row.
- Screenshot `/tmp/config-status-slot-check.png` shows the count attached to the icon without clipping.

- [ ] **Step 6: Verify interactions in the browser**

In the live browser at `http://localhost:5028/configuration/state`:

- Click the runtime validation icon.
- Expected: runtime validation popover opens.
- Close it.
- Click the reload status icon.
- Expected: reload status popover opens.
- Close it.

- [ ] **Step 7: Confirm final worktree state**

Run:

```bash
git status --short
```

Expected: no output. A non-empty status means a previous step changed files after the task commits; inspect the diff and create a focused follow-up commit before finishing.
