# Offline/Intranet Requirements for MoLibrary UI

## Overview

All UI modules and components in MoLibrary must support offline/intranet environments. This document provides guidelines for ensuring UI components work without internet access.

## Core Requirements

### 1. No Online Font CDN

**Forbidden:**
- Never directly reference Google Fonts, Adobe Fonts, or any online font services
- Never use `@import url('https://fonts.googleapis.com/...')` in CSS
- Never use `<link href="https://fonts.gstatic.com/..." />` in HTML

**Required:**
- All font files must be stored locally in `wwwroot/fonts/` directory
- Use local `@font-face` rules to reference fonts

### 2. Local Font Storage

All fonts must be stored in the project's `wwwroot/fonts/` directory:

```
MoLibrary.UI/
└── wwwroot/
    └── fonts/
        ├── roboto/
        │   ├── roboto-regular.woff2
        │   ├── roboto-medium.woff2
        │   └── roboto-bold.woff2
        ├── inter/
        │   ├── inter-regular.woff2
        │   └── inter-medium.woff2
        └── icons/
            └── material-icons.woff2
```

### 3. No External CDN

All static resources must be local:
- CSS files
- JavaScript files
- Images
- Icons
- Any other assets

**Forbidden:**
```html
<!-- Never use external CDN -->
<link href="https://cdn.jsdelivr.net/..." rel="stylesheet" />
<script src="https://cdnjs.cloudflare.com/..."></script>
```

**Required:**
```html
<!-- Use local resources -->
<link href="_content/MoLibrary.UI/css/theme.css" rel="stylesheet" />
<script src="_content/MoLibrary.UI/js/app.js"></script>
```

### 4. Intranet Compatibility

Must consider environments without internet access:
- Corporate intranets
- Air-gapped networks
- Development environments without internet
- Production deployments in restricted networks

## Font Management

### Font Downloader Tool

This skill includes a font downloader script at `scripts/font_downloader.py`.

**Usage:**

```bash
# Download a single font from Google Fonts CSS URL
python font_downloader.py "https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&display=swap"

# Download all project fonts (predefined in script)
python font_downloader.py --download-all

# Specify output directory
python font_downloader.py "CSS_URL" -o ./fonts

# Filter specific font weights
python font_downloader.py "CSS_URL" --weights 400,500,600
```

**Requirements:** Python 3.6+, requests library (`pip install requests`)

### Font Configuration

#### Step 1: Download fonts

Run the font downloader script to download required fonts from Google Fonts.

#### Step 2: Copy font files

Copy downloaded font files to the appropriate UI module's `wwwroot/fonts/` directory.

#### Step 3: Configure @font-face

Add `@font-face` rules in theme CSS to reference local font files:

```css
@font-face {
    font-family: 'Roboto';
    font-style: normal;
    font-weight: 400;
    font-display: swap;
    src: url('../fonts/roboto/roboto-regular.woff2') format('woff2');
}

@font-face {
    font-family: 'Roboto';
    font-style: normal;
    font-weight: 500;
    font-display: swap;
    src: url('../fonts/roboto/roboto-medium.woff2') format('woff2');
}

@font-face {
    font-family: 'Roboto';
    font-style: normal;
    font-weight: 700;
    font-display: swap;
    src: url('../fonts/roboto/roboto-bold.woff2') format('woff2');
}
```

### Font File Formats

Use `woff2` format as the primary format for best compression and browser support:

```css
@font-face {
    font-family: 'Inter';
    src: url('../fonts/inter/inter-regular.woff2') format('woff2'),
         url('../fonts/inter/inter-regular.woff') format('woff');
}
```

## MudBlazor Icons

MudBlazor uses Material Design Icons. Ensure the icon font is bundled locally:

### Icon Font Location

```
wwwroot/
└── fonts/
    └── materialdesignicons-webfont.woff2
```

### Icon Font CSS

```css
@font-face {
    font-family: 'Material Design Icons';
    src: url('../fonts/materialdesignicons-webfont.woff2') format('woff2');
    font-weight: normal;
    font-style: normal;
}
```

## Static Asset Checklist

Before deploying, verify:

- [ ] All fonts are stored in `wwwroot/fonts/`
- [ ] All CSS files are local (no external `@import`)
- [ ] All JavaScript files are local
- [ ] All images are local
- [ ] All icon fonts are local
- [ ] No external CDN references in HTML/Razor
- [ ] No external CDN references in CSS
- [ ] No external CDN references in JavaScript

## Testing Offline Compatibility

### Method 1: Network Disconnection

1. Disconnect from the internet
2. Run the application
3. Verify all UI renders correctly
4. Check browser console for failed resource loads

### Method 2: Browser DevTools

1. Open browser DevTools
2. Go to Network tab
3. Check "Disable cache"
4. Select "Offline" throttling
5. Reload the page
6. Verify no external requests fail

### Method 3: Hosts File Block

Add entries to hosts file to block common CDNs:

```
# Block CDNs for testing
127.0.0.1 fonts.googleapis.com
127.0.0.1 fonts.gstatic.com
127.0.0.1 cdn.jsdelivr.net
127.0.0.1 cdnjs.cloudflare.com
```

## Common Issues and Solutions

### Issue: Missing Font

**Symptom:** Text appears in fallback font (e.g., Times New Roman)

**Solution:**
1. Check if font file exists in `wwwroot/fonts/`
2. Verify `@font-face` rule in CSS
3. Check path in `src: url(...)` is correct
4. Ensure font file is copied during build

### Issue: Missing Icons

**Symptom:** Icons appear as empty boxes or text

**Solution:**
1. Verify icon font file exists
2. Check `@font-face` rule for icon font
3. Ensure correct font-family is used in CSS

### Issue: Broken Images

**Symptom:** Images show broken image icon

**Solution:**
1. Verify image file exists in `wwwroot/`
2. Check image path in HTML/CSS
3. Ensure image is included in project

### Issue: JavaScript Error

**Symptom:** Console shows failed script load

**Solution:**
1. Verify script file exists locally
2. Check script path in HTML
3. Remove any external script references

## Build Verification

Add a build step to verify no external references:

```bash
# Search for external URLs in CSS files
grep -r "https://" --include="*.css" wwwroot/

# Search for external URLs in Razor files
grep -r "https://" --include="*.razor" .

# Search for external URLs in HTML files
grep -r "https://" --include="*.html" .
```

Any results should be reviewed and potentially replaced with local resources.

## Module-Specific Notes

### MoLibrary.UI

Main UI module containing core themes and styles. All base fonts and icon fonts should be here.

### MoLibrary.Framework.UI

Framework-level UI components. Should reference fonts from MoLibrary.UI or include its own copies.

### MoLibrary.JobScheduler.UI

Job scheduler UI module. Should ensure all charting/visualization libraries are local.

### MoLibrary.Configuration.UI

Configuration dashboard. Should ensure all form and data display components work offline.

## Font Fallback Strategy

Define appropriate fallback fonts:

```css
body {
    font-family: 'Roboto', 'Segoe UI', 'Helvetica Neue', Arial, sans-serif;
}

.code {
    font-family: 'JetBrains Mono', 'Cascadia Code', 'Consolas', monospace;
}
```

This ensures readable text even if primary font fails to load.

## Summary

1. **Never use external CDNs** - All resources must be local
2. **Download and bundle fonts** - Use font downloader tool
3. **Test offline** - Verify UI works without internet
4. **Check build output** - No external URL references
5. **Use fallback fonts** - Ensure readable text in all cases
