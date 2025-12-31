# MudBlazor CSS Variables Reference

This document provides a complete reference for MudBlazor CSS variables used in theming.

## Palette Variables

### Primary Colors

| Name | Default Light | Default Dark | CSS Variable |
|------|---------------|--------------|--------------|
| Black | rgba(39,44,52,1) | rgba(39,39,47,1) | --mud-palette-black |
| White | rgba(255,255,255,1) | - | --mud-palette-white |
| Primary | rgba(89,74,226,1) | rgba(119,107,231,1) | --mud-palette-primary |
| PrimaryContrastText | rgba(255,255,255,1) | - | --mud-palette-primary-text |
| Secondary | rgba(255,64,129,1) | - | --mud-palette-secondary |
| SecondaryContrastText | rgba(255,255,255,1) | - | --mud-palette-secondary-text |
| Tertiary | rgba(30,200,165,1) | - | --mud-palette-tertiary |
| TertiaryContrastText | rgba(255,255,255,1) | - | --mud-palette-tertiary-text |

### Status Colors

| Name | Default Light | Default Dark | CSS Variable |
|------|---------------|--------------|--------------|
| Info | rgba(33,150,243,1) | rgba(50,153,255,1) | --mud-palette-info |
| InfoContrastText | rgba(255,255,255,1) | - | --mud-palette-info-text |
| Success | rgba(0,200,83,1) | rgba(11,186,131,1) | --mud-palette-success |
| SuccessContrastText | rgba(255,255,255,1) | - | --mud-palette-success-text |
| Warning | rgba(255,152,0,1) | rgba(255,168,0,1) | --mud-palette-warning |
| WarningContrastText | rgba(255,255,255,1) | - | --mud-palette-warning-text |
| Error | rgba(244,67,54,1) | rgba(246,78,98,1) | --mud-palette-error |
| ErrorContrastText | rgba(255,255,255,1) | - | --mud-palette-error-text |
| Dark | rgba(66,66,66,1) | rgba(39,39,47,1) | --mud-palette-dark |
| DarkContrastText | rgba(255,255,255,1) | - | --mud-palette-dark-text |

### Text Colors

| Name | Default Light | Default Dark | CSS Variable |
|------|---------------|--------------|--------------|
| TextPrimary | rgba(66,66,66,1) | rgba(255,255,255,0.7) | --mud-palette-text-primary |
| TextSecondary | rgba(0,0,0,0.54) | rgba(255,255,255,0.5) | --mud-palette-text-secondary |
| TextDisabled | rgba(0,0,0,0.38) | rgba(255,255,255,0.2) | --mud-palette-text-disabled |

### Action Colors

| Name | Default Light | Default Dark | CSS Variable |
|------|---------------|--------------|--------------|
| ActionDefault | rgba(0,0,0,0.54) | rgba(173,173,177,1) | --mud-palette-action-default |
| ActionDisabled | rgba(0,0,0,0.26) | rgba(255,255,255,0.26) | --mud-palette-action-disabled |
| ActionDisabledBackground | rgba(0,0,0,0.12) | rgba(255,255,255,0.12) | --mud-palette-action-disabled-background |

### Background Colors

| Name | Default Light | Default Dark | CSS Variable |
|------|---------------|--------------|--------------|
| Background | rgba(255,255,255,1) | rgba(50,51,61,1) | --mud-palette-background |
| BackgroundGray | rgba(245,245,245,1) | rgba(39,39,47,1) | --mud-palette-background-gray |
| Surface | rgba(255,255,255,1) | rgba(55,55,64,1) | --mud-palette-surface |
| DrawerBackground | rgba(255,255,255,1) | rgba(39,39,47,1) | --mud-palette-drawer-background |
| DrawerText | rgba(66,66,66,1) | rgba(255,255,255,0.5) | --mud-palette-drawer-text |
| DrawerIcon | rgba(97,97,97,1) | rgba(255,255,255,0.5) | --mud-palette-drawer-icon |
| AppbarBackground | rgba(89,74,226,1) | rgba(39,39,47,1) | --mud-palette-appbar-background |
| AppbarText | rgba(255,255,255,1) | rgba(255,255,255,0.7) | --mud-palette-appbar-text |

### Lines and Borders

| Name | Default Light | Default Dark | CSS Variable |
|------|---------------|--------------|--------------|
| LinesDefault | rgba(0,0,0,0.12) | rgba(255,255,255,0.12) | --mud-palette-lines-default |
| LinesInputs | rgba(189,189,189,1) | rgba(255,255,255,0.3) | --mud-palette-lines-inputs |
| TableLines | rgba(224,224,224,1) | rgba(255,255,255,0.12) | --mud-palette-table-lines |
| TableStriped | rgba(0,0,0,0.02) | rgba(255,255,255,0.2) | --mud-palette-table-striped |
| TableHover | rgba(0,0,0,0.04) | - | --mud-palette-table-hover |
| Divider | rgba(224,224,224,1) | rgba(255,255,255,0.12) | --mud-palette-divider |
| DividerLight | rgba(0,0,0,0.8) | rgba(255,255,255,0.06) | --mud-palette-divider-light |
| Skeleton | rgba(0,0,0,0.11) | rgba(255,255,255,0.11) | --mud-palette-skeleton |

### Color Variants

| Name | Default Light | Default Dark | CSS Variable |
|------|---------------|--------------|--------------|
| PrimaryDarken | rgb(62,44,221) | rgb(90,75,226) | --mud-palette-primary-darken |
| PrimaryLighten | rgb(118,106,231) | rgb(151,141,236) | --mud-palette-primary-lighten |
| SecondaryDarken | rgb(255,31,105) | - | --mud-palette-secondary-darken |
| SecondaryLighten | rgb(255,102,153) | - | --mud-palette-secondary-lighten |
| TertiaryDarken | rgb(25,169,140) | - | --mud-palette-tertiary-darken |
| TertiaryLighten | rgb(42,223,187) | - | --mud-palette-tertiary-lighten |
| InfoDarken | rgb(12,128,223) | rgb(10,133,255) | --mud-palette-info-darken |
| InfoLighten | rgb(71,167,245) | rgb(92,173,255) | --mud-palette-info-lighten |
| SuccessDarken | rgb(0,163,68) | rgb(9,154,108) | --mud-palette-success-darken |
| SuccessLighten | rgb(0,235,98) | rgb(13,222,156) | --mud-palette-success-lighten |
| WarningDarken | rgb(214,129,0) | rgb(214,143,0) | --mud-palette-warning-darken |
| WarningLighten | rgb(255,167,36) | rgb(255,182,36) | --mud-palette-warning-lighten |
| ErrorDarken | rgb(242,28,13) | rgb(244,47,70) | --mud-palette-error-darken |
| ErrorLighten | rgb(246,96,85) | rgb(248,119,134) | --mud-palette-error-lighten |
| DarkDarken | rgb(46,46,46) | rgb(23,23,28) | --mud-palette-dark-darken |
| DarkLighten | rgb(87,87,87) | rgb(56,56,67) | --mud-palette-dark-lighten |

### Opacity and Effects

| Name | Default | CSS Variable |
|------|---------|--------------|
| BorderOpacity | 1 | --mud-palette-border-opacity |
| HoverOpacity | 0.06 | --mud-palette-hover-opacity |
| RippleOpacity | 0.1 | --mud-palette-ripple-opacity |
| RippleOpacitySecondary | 0.2 | --mud-palette-ripple-opacity-secondary |

### Gray Scale

| Name | Default | CSS Variable |
|------|---------|--------------|
| GrayDefault | #9E9E9E | --mud-palette-gray-default |
| GrayLight | #BDBDBD | --mud-palette-gray-light |
| GrayLighter | #E0E0E0 | --mud-palette-gray-lighter |
| GrayDark | #757575 | --mud-palette-gray-dark |
| GrayDarker | #616161 | --mud-palette-gray-darker |

### Overlay Colors

| Name | Default | CSS Variable |
|------|---------|--------------|
| OverlayDark | rgba(33,33,33,0.5) | --mud-palette-overlay-dark |
| OverlayLight | rgba(255,255,255,0.5) | --mud-palette-overlay-light |

## Shadow/Elevation Variables

| Name | CSS Variable |
|------|--------------|
| Elevation[0] | --mud-elevation-0 |
| Elevation[1] | --mud-elevation-1 |
| Elevation[2] | --mud-elevation-2 |
| Elevation[3] | --mud-elevation-3 |
| Elevation[4] | --mud-elevation-4 |
| Elevation[5] | --mud-elevation-5 |
| Elevation[6] | --mud-elevation-6 |
| Elevation[7] | --mud-elevation-7 |
| Elevation[8] | --mud-elevation-8 |
| Elevation[9] | --mud-elevation-9 |
| Elevation[10] | --mud-elevation-10 |
| Elevation[11] | --mud-elevation-11 |
| Elevation[12] | --mud-elevation-12 |
| Elevation[13] | --mud-elevation-13 |
| Elevation[14] | --mud-elevation-14 |
| Elevation[15] | --mud-elevation-15 |
| Elevation[16] | --mud-elevation-16 |
| Elevation[17] | --mud-elevation-17 |
| Elevation[18] | --mud-elevation-18 |
| Elevation[19] | --mud-elevation-19 |
| Elevation[20] | --mud-elevation-20 |
| Elevation[21] | --mud-elevation-21 |
| Elevation[22] | --mud-elevation-22 |
| Elevation[23] | --mud-elevation-23 |
| Elevation[24] | --mud-elevation-24 |
| Elevation[25] | --mud-elevation-25 |

## Layout Properties

| Name | Default | CSS Variable |
|------|---------|--------------|
| DefaultBorderRadius | 4px | --mud-default-borderradius |
| DrawerMiniWidthLeft | 56px | --mud-drawer-mini-width-left |
| DrawerMiniWidthRight | 56px | --mud-drawer-mini-width-right |
| DrawerWidthLeft | 240px | --mud-drawer-width-left |
| DrawerWidthRight | 240px | --mud-drawer-width-right |
| AppbarHeight | 64px | --mud-appbar-height |

## Typography Variables

### Default Typography

| Property | Default | CSS Variable |
|----------|---------|--------------|
| FontFamily | Roboto, Helvetica, Arial, sans-serif | --mud-typography-default-family |
| FontWeight | 400 | --mud-typography-default-weight |
| FontSize | .875rem | --mud-typography-default-size |
| LineHeight | 1.43 | --mud-typography-default-lineheight |
| LetterSpacing | .01071em | --mud-typography-default-letterspacing |
| TextTransform | none | --mud-typography-default-text-transform |

### Heading Typography (H1-H6)

| Element | Weight | Size | Line Height | Letter Spacing | CSS Variable Prefix |
|---------|--------|------|-------------|----------------|---------------------|
| H1 | 300 | 6rem | 1.167 | -.01562em | --mud-typography-h1-* |
| H2 | 300 | 3.75rem | 1.2 | -.00833em | --mud-typography-h2-* |
| H3 | 400 | 3rem | 1.167 | 0 | --mud-typography-h3-* |
| H4 | 400 | 2.125rem | 1.235 | .00735em | --mud-typography-h4-* |
| H5 | 400 | 1.5rem | 1.334 | 0 | --mud-typography-h5-* |
| H6 | 500 | 1.25rem | 1.6 | .0075em | --mud-typography-h6-* |

### Body and Other Typography

| Element | Weight | Size | Line Height | Letter Spacing | CSS Variable Prefix |
|---------|--------|------|-------------|----------------|---------------------|
| Subtitle1 | 400 | 1rem | 1.75 | .00938em | --mud-typography-subtitle1-* |
| Subtitle2 | 500 | .875rem | 1.57 | .00714em | --mud-typography-subtitle2-* |
| Body1 | 400 | 1rem | 1.5 | .00938em | --mud-typography-body1-* |
| Body2 | 400 | .875rem | 1.43 | .01071em | --mud-typography-body2-* |
| Button | 500 | .875rem | 1.75 | .02857em | --mud-typography-button-* |
| Caption | 400 | .75rem | 1.66 | .03333em | --mud-typography-caption-* |
| Overline | 400 | .75rem | 2.66 | .08333em | --mud-typography-overline-* |

## ZIndex Variables

| Name | Default | CSS Variable |
|------|---------|--------------|
| Drawer | 1100 | --mud-zindex-drawer |
| Popover | 1200 | --mud-zindex-popover |
| AppBar | 1300 | --mud-zindex-appbar |
| Dialog | 1400 | --mud-zindex-dialog |
| Snackbar | 1500 | --mud-zindex-snackbar |
| Tooltip | 1600 | --mud-zindex-tooltip |

## Common Usage Examples

### Using Palette Variables

```css
/* Background and text colors */
.my-component {
    background-color: var(--mud-palette-surface);
    color: var(--mud-palette-text-primary);
}

/* Status colors */
.success-indicator {
    color: var(--mud-palette-success);
}

.error-message {
    color: var(--mud-palette-error);
    background-color: var(--mud-palette-error-lighten);
}

/* Borders and dividers */
.card-border {
    border: 1px solid var(--mud-palette-lines-default);
}

.input-border {
    border-color: var(--mud-palette-lines-inputs);
}
```

### Using Elevation Variables

```css
.elevated-card {
    box-shadow: var(--mud-elevation-4);
}

.dialog-shadow {
    box-shadow: var(--mud-elevation-24);
}
```

### Using Typography Variables

```css
.custom-heading {
    font-family: var(--mud-typography-h1-family);
    font-weight: var(--mud-typography-h1-weight);
    font-size: var(--mud-typography-h1-size);
    line-height: var(--mud-typography-h1-lineheight);
}
```

### Using Layout Variables

```css
.custom-drawer {
    width: var(--mud-drawer-width-left);
}

.custom-appbar {
    height: var(--mud-appbar-height);
}

.rounded-element {
    border-radius: var(--mud-default-borderradius);
}
```

## Best Practices

1. **Always use CSS variables** instead of hardcoded colors for theme compatibility
2. **Use palette contrast text** for text on colored backgrounds (e.g., `--mud-palette-primary-text` on `--mud-palette-primary`)
3. **Consider dark mode** by testing with both light and dark palette variables
4. **Use semantic variables** (like `--mud-palette-error`) instead of color variables directly
5. **Layer elevations appropriately** - higher elements should have higher elevation values
