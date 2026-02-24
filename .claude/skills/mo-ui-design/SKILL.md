---
name: mo-ui-design
description: This skill should be used when the user asks to "design UI", "prototype UI", "UI mockup", "create UI design", "interactive MVP", "UI prototype", "design page layout", "wireframe", "rapid prototype", "ui-design", "design module UI", "preview UI", "UI设计", "设计UI", "原型设计", "UI原型", "界面设计", "模块设计", or needs to create an interactive HTML prototype before implementing a MudBlazor/Blazor UI module.
version: 1.0.0
---

# Monica UI Design Prototyping

Create interactive HTML prototypes using Tailwind CSS to visualize UI designs before MudBlazor implementation.

## Workflow

| Phase | Action | Output |
|-------|--------|--------|
| 1. Gather | Ask about feature requirements and interactions | Understanding |
| 2. Design | Break UI into modules, plan component mapping | `design.md` |
| 3. Prototype | Build interactive HTML with Tailwind CSS | `index.html` |
| 4. Iterate | Refine based on user feedback | Updated files |

## Phase 1: Requirements Gathering

When the user invokes this skill, ask 2-3 focused questions per round:

1. **Feature name** — Used for folder naming (kebab-case)
2. **Purpose** — What does this UI do? Who uses it?
3. **Key interactions** — CRUD, monitoring, configuration, chat, dashboard, etc.
4. **Layout needs** — Sidebar? Tabs? Data grids? Dialogs? Tree views?
5. **Reference pages** — Any existing Monica pages to draw inspiration from?

Keep it conversational. Don't ask all questions at once.

## Phase 2: Design Document

### Folder Setup

1. Create `.ui-design/` at project root if it does not exist
2. Create `.ui-design/{feature-name}/` (kebab-case)
3. Create `design.md` using the template from `references/design-template.md`

### Content Requirements

The `design.md` must contain:
- Feature overview and goals
- Module/section breakdown with visual hierarchy
- Component mapping table (prototype element → MudBlazor component)
- Key interaction flows
- Responsive behavior notes

## Phase 3: Interactive Prototype

### Output Structure

```
.ui-design/{feature-name}/
├── design.md      # Design document
└── index.html     # Interactive prototype
```

### Prototype Rules

Build a single `index.html` that:

1. **Uses Tailwind CSS via CDN** with inline config mapping Monica's palette:

```html
<script src="https://cdn.tailwindcss.com"></script>
<script>
tailwind.config = {
  darkMode: ['class', '[data-theme="dark"]'],
  theme: {
    extend: {
      colors: {
        primary: { DEFAULT: '#1976d2', light: '#42a5f5', dark: '#1565c0' },
        secondary: { DEFAULT: '#dc004e', light: '#ff5983', dark: '#9a0036' },
        info: { DEFAULT: '#2196f3', light: '#64b5f6', dark: '#1976d2' },
        success: { DEFAULT: '#4caf50', light: '#81c784', dark: '#388e3c' },
        warning: { DEFAULT: '#ff9800', light: '#ffb74d', dark: '#f57c00' },
        error: { DEFAULT: '#f44336', light: '#e57373', dark: '#d32f2f' },
        surface: { DEFAULT: '#ffffff', dark: '#1e1e1e' },
        background: { DEFAULT: '#fafafa', dark: '#121212' },
        appbar: { DEFAULT: '#f8f9fa', dark: '#2d2d30' },
      },
      fontFamily: {
        sans: ['Roboto', 'Helvetica', 'Arial', 'sans-serif'],
      },
    },
  },
}
</script>
```

2. **Includes dark/light theme toggle** with inline JS:

```html
<script>
  const html = document.documentElement;
  const saved = localStorage.getItem('mo-theme');
  if (saved === 'dark' || (!saved && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
    html.classList.add('dark');
    html.setAttribute('data-theme', 'dark');
  }
  function toggleTheme() {
    const isDark = html.classList.toggle('dark');
    html.setAttribute('data-theme', isDark ? 'dark' : 'light');
    localStorage.setItem('mo-theme', isDark ? 'dark' : 'light');
  }
</script>
```

3. **Is fully interactive** — Tabs switch content, dialogs open/close, navigation highlights, forms accept input. All JS inline in `<script>` tags.

4. **Is responsive** — Use Tailwind breakpoints (`sm:`, `md:`, `lg:`, `xl:`).

5. **Focuses on layout and module arrangement** — Show the real structure, sections, and interaction flow. Use placeholder data that feels realistic.

6. **Uses good typography** — Load Roboto from Google Fonts CDN:
```html
<link href="https://fonts.googleapis.com/css2?family=Roboto:wght@300;400;500;700&display=swap" rel="stylesheet">
```

7. **Uses Lucide icons** (optional) for clean iconography:
```html
<script src="https://unpkg.com/lucide@latest"></script>
<script>lucide.createIcons();</script>
```

### Dark Mode Pattern

Use Tailwind's `dark:` variant for all theme-sensitive styles:

```html
<div class="bg-background dark:bg-background-dark text-gray-900 dark:text-gray-100">
  <div class="bg-surface dark:bg-surface-dark rounded-lg shadow">
    <!-- content -->
  </div>
</div>
```

## Phase 4: Iteration

When the user provides feedback:
1. Update `index.html` with the requested changes
2. Update `design.md` if the module structure or component mapping changed
3. Summarize what changed

## Component Mapping Reference

When building the prototype, keep in mind the eventual MudBlazor implementation:

| Prototype Pattern | MudBlazor Component | Notes |
|-------------------|---------------------|-------|
| Top navbar | `MudAppBar` + `MudToolBar` | Monica uses top-nav, not sidebar |
| Side panel | `MudDrawer` | Per-page sidebars (e.g., session list) |
| Tab bar | `MudTabs` + `MudTabPanel` | Content switching |
| Data table | `MudDataGrid` | Sorting, filtering, pagination |
| Card | `MudCard` + `MudCardContent` | Content containers |
| Dialog/Modal | `MudDialog` | Via `IDialogService` |
| Tree view | `MudTreeView` + `TreeItemData<T>` | Hierarchical data |
| Breadcrumbs | `MudBreadcrumbs` + `BreadcrumbItem` | Navigation trail |
| Text input | `MudTextField` | With validation support |
| Select dropdown | `MudSelect` + `MudSelectItem` | Single/multi select |
| Button | `MudButton` / `MudIconButton` | Various variants |
| Chip | `MudChip` | Status indicators, tags |
| Menu | `MudMenu` + `MudMenuItem` | Dropdown menus |
| Progress | `MudProgressLinear` / `MudProgressCircular` | Loading states |
| Tooltip | `MudTooltip` | Hover information |
| Snackbar | `ISnackbar` | Toast notifications |
