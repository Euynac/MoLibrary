# Monica Docs & Official Website Prototype

## 1. Design Thinking

| Dimension | Decision |
|-----------|----------|
| **Purpose** | Provide a striking, professional entry point (Landing Page) and a highly readable, immersive documentation experience for the Monica framework, appealing to .NET developers and enterprise architects. |
| **Aesthetic Direction** | **"Dark Tech & Deep Violet"**<br>A sophisticated dark mode using deep charcoal and obsidian blacks, energized by the vibrant, electric purple/violet from the Monica logo. The style is modern, slightly brutalist in its geometric precision, but softened by glowing gradients and glassmorphism. |
| **Signature Detail** | **The Violet Halo & Code Luminescence.** Interactive elements cast a subtle violet glow. Code blocks look like high-end terminal windows with glass borders and syntax highlighting that pops against the dark void. |
| **Constraints** | Must be a static HTML/CSS/JS prototype. Responsive for mobile and desktop. High contrast for documentation readability. |

## 2. Feature Overview

- **Landing Page (`index.html`)**: 
  - **Hero Section**: Strong value proposition, primary "Get Started" CTA, secondary "Read Docs" CTA, and a glowing, interactive terminal window demonstrating Monica's simplicity.
  - **Features Grid**: Glassmorphic cards highlighting Core concepts (DDD, Modularity, Res Pattern).
  - **Footer**: Simple links to GitHub, Nuget, etc.
- **Documentation Page (`docs.html`)**:
  - **Layout**: Classic 3-pane docs layout. Left: Navigation Tree. Center: Content Area. Right: Table of Contents (TOC).
  - **Typography**: High readability, generous line height, clear hierarchy (`h1` to `h6`), with `JetBrains Mono` for code snippets.
  - **Components**: Copyable code blocks, info/warning callouts, breadcrumbs.

## 3. Visual Language

- **Typography**: 
  - Headings: `Outfit` (Geometric, modern, bold).
  - Body: `Inter` (Highly legible, clean).
  - Monospace: `JetBrains Mono` (Developer-centric).
- **Color Palette**:
  - Background: `#09090b` (Zinc 950)
  - Surface: `#18181b` (Zinc 900)
  - Primary Accent: `#6d28d9` (Violet 700) to `#8b5cf6` (Violet 500) - inspired by the logo.
  - Text: `#f4f4f5` (Zinc 100) for high emphasis, `#a1a1aa` (Zinc 400) for secondary.
- **Motion**:
  - Cards lift on hover with a violet shadow (`box-shadow: 0 10px 40px -10px rgba(139, 92, 246, 0.3)`).
  - Smooth transitions on buttons and links.

## 4. File Structure

- `index.html` - The Landing Page.
- `docs.html` - The Documentation Page.
- `styles.css` - Custom styling for glows, glassmorphism, and markdown typography.
- `app.js` - Lightweight interactions (mobile menu, sidebar toggle, copy-to-clipboard).
