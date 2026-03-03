---
name: mo-ui-design
description: This skill should be used when the user asks to "design UI", "prototype UI", "UI mockup", "create UI design", "interactive MVP", "UI prototype", "design page layout", "wireframe", "rapid prototype", "ui-design", "design module UI", "preview UI", "UI设计", "设计UI", "原型设计", "UI原型", "界面设计", "模块设计", or needs to create an interactive HTML prototype before implementing a MudBlazor/Blazor UI module.
version: 2.1.0
---

# Monica UI Design Prototyping

Create interactive HTML prototypes with bold, distinctive aesthetics to visualize UI designs before MudBlazor implementation.

## CRITICAL: Design-Only Session Rule

When this skill is active, the session is in **design mode**. Focus exclusively on UI design and prototyping:

- Do NOT write any Blazor/C#/MudBlazor implementation code
- Do NOT create `.razor`, `.razor.cs`, `.razor.css`, or any `.cs` files
- Do NOT discuss implementation details, service layers, or backend concerns
- Only produce: `design.md`, `index.html`, and design-related conversation
- If the user asks to start coding, remind them to begin a new session with `/mo-ui-development`

Design first, code later.

## Workflow

| Phase | Action | Output |
|-------|--------|--------|
| 1. Discover | Gather requirements and commit to an aesthetic direction | Understanding + Design Thinking |
| 2. Design | Break UI into modules, plan layout and interactions | `design.md` |
| 3. Prototype | Build interactive HTML with distinctive aesthetics | `index.html` |
| 4. Iterate | Refine based on user feedback | Updated files |

## Phase 1: Discovery and Design Thinking

### Requirements Gathering

Ask 2-3 focused questions per round:

1. **Feature name** — Used for folder naming (kebab-case)
2. **Purpose** — What does this UI do? Who uses it?
3. **Key interactions** — CRUD, monitoring, configuration, chat, dashboard, etc.
4. **Layout needs** — Sidebar? Tabs? Data grids? Dialogs? Tree views?
5. **Reference pages** — Any existing Monica pages to draw inspiration from?

Keep it conversational. Do not ask all questions at once.

### Design Thinking

Before any design work, commit to a bold aesthetic direction by answering:

| Dimension | Question |
|-----------|----------|
| **Purpose** | What problem does this interface solve? Who uses it? |
| **Aesthetic Direction** | Pick a bold tone: brutally minimal, maximalist chaos, retro-futuristic, organic/natural, luxury/refined, playful/toy-like, editorial/magazine, brutalist/raw, art deco/geometric, soft/pastel, industrial/utilitarian, or something entirely unique |
| **Signature Detail** | What makes this design UNFORGETTABLE? The one thing someone will remember |
| **Constraints** | Technical requirements (framework, performance, accessibility) |

Present the aesthetic direction to the user for confirmation before proceeding. The key is intentionality — bold maximalism and refined minimalism both work when executed with precision.

## Phase 2: Design Document

### Folder Setup

1. Create `.ui-design/` at project root if it does not exist
2. Create `.ui-design/{feature-name}/` (kebab-case)
3. Create `design.md` using the template from `references/design-template.md`

### Content Requirements

The `design.md` must contain:
- Design Thinking table (aesthetic direction, typography, color, signature detail)
- Feature overview and goals
- Module/section breakdown with visual hierarchy
- Motion and animation plan
- Key interaction flows
- Responsive behavior notes

## Phase 3: Interactive Prototype

### Output Structure

```
.ui-design/{feature-name}/
├── design.md      # Design document
└── index.html     # Interactive prototype
```

### Aesthetic Execution Rules

Every prototype must be visually distinctive. Consult `references/aesthetics-guidelines.md` for detailed guidance on:

- **Typography**: Choose unique, characterful fonts. Never default to Arial, Inter, Roboto, or system fonts. Pair a distinctive display font with a refined body font. Load from Google Fonts CDN.
- **Color**: Commit to a cohesive palette matching the aesthetic direction. Dominant colors with sharp accents outperform timid, evenly-distributed palettes.
- **Motion**: Focus on high-impact moments — one well-orchestrated page load with staggered reveals creates more delight than scattered micro-interactions. Add surprising hover states and smooth transitions.
- **Spatial Composition**: Break free from predictable grids. Use asymmetry, overlap, diagonal flow, grid-breaking elements, generous negative space or controlled density.
- **Atmosphere**: Create depth with gradient meshes, noise textures, geometric patterns, layered transparencies, dramatic shadows, decorative borders, custom scrollbars.

No two designs should look the same. Vary themes, fonts, and aesthetics across projects.

### Prototype Technical Rules

Build a single `index.html` that:

1. **Uses Tailwind CSS via CDN** with custom config extending colors and fonts to match the chosen aesthetic direction

2. **Is fully interactive** — Tabs switch content, dialogs open/close, navigation highlights, forms accept input. All JS inline.

3. **Is responsive** — Use Tailwind breakpoints (`sm:`, `md:`, `lg:`, `xl:`).

4. **Includes meaningful animations** — Page load reveals, hover effects, transitions matching the aesthetic direction. CSS-only preferred.

5. **Uses realistic placeholder data** — Show the real structure, sections, and interaction flow with data that feels authentic.

6. **Uses Lucide icons** (optional) for iconography:
```html
<script src="https://unpkg.com/lucide@latest"></script>
<script>lucide.createIcons();</script>
```

## Phase 4: Iteration

When the user provides feedback:
1. Update `index.html` with the requested changes
2. Update `design.md` if the module structure changed
3. Summarize what changed
4. Do NOT transition to implementation code — stay in design mode

## Additional Resources

### Reference Files

- **`references/design-template.md`** — Design document template with Design Thinking section
- **`references/aesthetics-guidelines.md`** — Detailed frontend aesthetics: typography pairings, color theory, motion patterns, spatial composition, atmosphere techniques, and aesthetic direction reference table
