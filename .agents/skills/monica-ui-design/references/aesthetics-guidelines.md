# Frontend Aesthetics Guidelines

## Core Principle

Every design must have a clear, intentional aesthetic point-of-view. Bold maximalism and refined minimalism both work — the key is intentionality, not intensity. Match implementation complexity to the aesthetic vision.

## Typography

Choose fonts that are beautiful, unique, and characterful. Pair a distinctive display font with a refined body font.

### Font Selection Rules

- Never default to generic fonts: Arial, Inter, Roboto, system fonts
- Never converge on overused "modern" choices like Space Grotesk across designs
- Each design should have its own typographic identity
- Load from Google Fonts CDN or other free font CDNs

### Pairing Examples by Aesthetic

| Aesthetic | Display Font | Body Font |
|-----------|-------------|-----------|
| Editorial/Magazine | Playfair Display | Source Serif Pro |
| Brutalist/Raw | JetBrains Mono | IBM Plex Sans |
| Luxury/Refined | Cormorant Garamond | Lato |
| Retro-Futuristic | Orbitron | Exo 2 |
| Playful/Toy-like | Fredoka One | Nunito |
| Art Deco/Geometric | Poiret One | Raleway |
| Soft/Pastel | Quicksand | DM Sans |
| Industrial/Utilitarian | Oswald | Barlow |

These are starting points — explore beyond these. Vary choices across designs.

## Color and Theme

Commit to a cohesive palette. Use CSS custom properties for consistency.

### Palette Construction

- Dominant color with sharp accents outperforms timid, evenly-distributed palettes
- Define palette as CSS variables or Tailwind config extensions
- Consider the emotional weight of the chosen aesthetic direction
- Dark and light themes should both feel intentional, not just inverted

### Anti-Patterns

- Purple gradients on white backgrounds (cliche AI aesthetic)
- Safe blue-gray corporate palettes without personality
- Rainbow palettes with no hierarchy
- Identical palettes across different designs

### Monica Base Palette (Starting Point)

The Monica palette provides functional colors. Extend or override based on aesthetic direction:

```
primary: #1976d2 / light: #42a5f5 / dark: #1565c0
secondary: #dc004e / light: #ff5983 / dark: #9a0036
info: #2196f3 / success: #4caf50 / warning: #ff9800 / error: #f44336
surface: #ffffff / dark: #1e1e1e
background: #fafafa / dark: #121212
```

Override freely when the aesthetic direction demands it. The prototype is for design exploration, not production fidelity.

## Motion and Animation

Focus on high-impact moments over scattered micro-interactions.

### Priority Techniques

1. **Page load orchestration** — Staggered reveals using `animation-delay` create delight
2. **Scroll-triggered animations** — Elements that animate into view on scroll
3. **Hover states that surprise** — Unexpected transforms, color shifts, reveals
4. **Transition choreography** — Coordinated transitions when switching views/tabs

### Implementation

- Prioritize CSS-only solutions (`@keyframes`, `transition`, `animation`)
- Use `IntersectionObserver` for scroll-triggered effects
- Keep animations performant: prefer `transform` and `opacity`
- Respect `prefers-reduced-motion` media query

### Example: Staggered Reveal

```css
.reveal-item {
  opacity: 0;
  transform: translateY(20px);
  animation: revealUp 0.6s ease forwards;
}
.reveal-item:nth-child(1) { animation-delay: 0.1s; }
.reveal-item:nth-child(2) { animation-delay: 0.2s; }
.reveal-item:nth-child(3) { animation-delay: 0.3s; }

@keyframes revealUp {
  to { opacity: 1; transform: translateY(0); }
}
```

## Spatial Composition

Break free from predictable grid layouts.

### Techniques

- **Asymmetry** — Unequal column splits, off-center focal points
- **Overlap** — Elements that break their containers, layered cards
- **Diagonal flow** — Angled sections, skewed backgrounds
- **Grid-breaking elements** — Items that span or escape the grid
- **Generous negative space** OR **controlled density** — both valid, commit to one
- **Depth through layering** — z-index stacking, shadows, blur effects

## Backgrounds and Visual Details

Create atmosphere and depth rather than defaulting to solid colors.

### Techniques

- Gradient meshes and multi-stop gradients
- Noise/grain textures (CSS or SVG filter)
- Geometric patterns (CSS-generated or SVG)
- Layered transparencies and glassmorphism
- Dramatic shadows (large, colored, layered)
- Decorative borders and dividers
- Custom scrollbar styling

### Grain Overlay Example

```css
.grain::after {
  content: "";
  position: fixed;
  inset: 0;
  opacity: 0.03;
  background-image: url("data:image/svg+xml,..."); /* noise SVG */
  pointer-events: none;
  z-index: 9999;
}
```

## Aesthetic Directions Reference

When committing to a direction, execute with full conviction:

| Direction | Key Traits | Typography Feel | Color Feel |
|-----------|-----------|-----------------|------------|
| Brutally Minimal | Extreme whitespace, single accent, stark contrast | Monospace or geometric sans | Black/white + one color |
| Maximalist Chaos | Dense, layered, colorful, energetic | Bold display + tight body | Rich, saturated, many |
| Retro-Futuristic | Neon, scanlines, terminal vibes, glow effects | Monospace or sci-fi display | Dark base + neon accents |
| Organic/Natural | Soft curves, earth tones, flowing shapes | Rounded serif or humanist sans | Warm earth palette |
| Luxury/Refined | Generous space, gold accents, elegant details | Thin serif + clean sans | Dark + metallic accents |
| Playful/Toy-like | Rounded corners, bouncy animations, bright | Rounded/bubbly display | Bright, saturated, fun |
| Editorial/Magazine | Strong typographic hierarchy, columns, pull quotes | Serif display + sans body | High contrast, limited |
| Brutalist/Raw | Exposed structure, harsh borders, raw HTML feel | System/mono, oversized | Harsh contrast, primary |
| Art Deco/Geometric | Symmetry, gold lines, geometric patterns | Geometric display + elegant body | Gold, black, deep jewel |
| Soft/Pastel | Gentle gradients, rounded shapes, airy | Light-weight rounded sans | Muted pastels, soft |
| Industrial/Utilitarian | Functional, dense data, no decoration | Condensed sans, tabular | Muted, functional grays |
