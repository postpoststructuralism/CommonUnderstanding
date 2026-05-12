# Government of Canada Design System Implementation

## Overview
This ASP.NET Core Razor Pages application has been themed with the Government of Canada (GC) Design System, featuring a professional, accessible, and bilingual-ready interface suitable for the Indian Land Registry System (ILRS) Portal.

## ?? Design System Components

### Color Palette
- **Primary Blue**: `#26374a` - Navigation, headers, primary buttons
- **Secondary Blue**: `#335075` - Hover states, accents
- **Background**: `#f8fafc` - Main content area
- **White**: `#ffffff` - Cards, panels
- **Border Light**: `#e5e7eb` - Borders and separators
- **Text Colors**: Dark `#1f2937`, Muted `#6b7280`

### CSS Variables
All colors are defined as CSS variables in `portal.css`:
```css
:root {
    --gc-primary-blue: #26374a;
    --gc-secondary-blue: #335075;
    --gc-background: #f8fafc;
    --gc-white: #ffffff;
    --gc-border-light: #e5e7eb;
    /* ... and more */
}
```

## ?? File Structure

```
CommonUnderstanding/
??? wwwroot/
?   ??? css/
?   ?   ??? portal.css          # GC Design System styles (NEW)
?   ?   ??? site.css            # Enhanced site styles (UPDATED)
?   ??? images/
?   ?   ??? sig-blk-en.svg      # GC Canada logo (NEW)
?   ?   ??? wmms-blk.svg        # Canada wordmark (NEW)
?   ??? js/
?       ??? site.js
??? Views/
?   ??? Shared/
?   ?   ??? _Layout.cshtml      # GC themed layout (UPDATED)
?   ??? Home/
?       ??? Index.cshtml        # GC component demo (UPDATED)
```

## ??? Key Layout Components

### 1. Sticky Header (`gc-header`)
- GC Canada logo (sig-blk-en.svg)
- Site title: "ILRS Portal" with subtitle
- Language toggle button (EN/FR)
- 4px solid primary blue bottom border
- Sticky positioning with shadow

### 2. Gradient Navigation Bar (`gc-nav`)
- Linear gradient: 135deg from primary to secondary blue
- White text with semi-transparent hover backgrounds
- Active state: white underline (3px box-shadow)
- Icon + text navigation items (FontAwesome)
- Responsive hamburger menu for mobile
- Dropdown support with white background

### 3. Main Content Area (`gc-main-content`)
- Container-fluid with horizontal padding
- Light gray background (`#f8fafc`)
- Breadcrumb navigation support
- Minimum height calculation for sticky footer

### 4. Footer (`gc-footer`)
- Same gradient as navigation
- Canada wordmark (inverted to white)
- Copyright, version, and powered-by text
- Footer links: Privacy, Terms, Contact
- Responsive flex layout

## ?? Component Classes

### Cards
```html
<div class="gc-card">
    <div class="gc-card-header">Header</div>
    <div class="gc-card-body">Content</div>
</div>
```
- White background with light border
- 12px border-radius
- Hover effect: shadow + translateY(-2px)

### Buttons
```html
<!-- Primary -->
<button class="gc-btn-primary">
    <i class="fas fa-icon"></i> Text
</button>

<!-- Secondary -->
<button class="gc-btn-secondary">Text</button>
```
- Primary: Primary blue background, white text
- Secondary: Transparent with blue border
- Hover: Color change + translateY(-1px)
- Focus: 2px outline with offset

### Forms
```html
<div class="gc-form-group">
    <label class="gc-form-label">Label</label>
    <input class="gc-form-control" type="text">
</div>
```
- 1px light border
- 0.375rem border-radius
- Focus: Blue border + shadow ring

### Tables
```html
<table class="gc-table">
    <thead><tr><th>Header</th></tr></thead>
    <tbody><tr><td>Data</td></tr></tbody>
</table>
```
- Primary blue header with white text
- Uppercase header labels
- Hover: #f7f8fa row background
- Full-width with rounded corners

### Badges
```html
<span class="gc-badge">Active</span>

<!-- Removable -->
<span class="gc-badge gc-badge-removable">
    Selected
    <button class="gc-badge-remove">&times;</button>
</span>
```
- Pill-shaped (border-radius: 50px)
- Primary blue background
- Removable variant with close button

### Breadcrumbs
```html
<nav aria-label="breadcrumb" class="gc-breadcrumb">
    <ol class="breadcrumb">
        <li class="breadcrumb-item"><a href="#">Home</a></li>
        <li class="breadcrumb-item active">Current</li>
    </ol>
</nav>
```
- Transparent background
- No border
- Active item in primary blue

### Page Headings
```html
<h1 class="gc-page-heading">Page Title</h1>
<h2 class="gc-section-heading">Section Title</h2>
```
- Page heading: 3px bottom border in primary blue
- Section heading: No border, smaller size

### Filter Panel
```html
<div class="gc-filter-panel">
    <!-- Filter controls -->
</div>
```
- Sticky positioning (top: 1rem)
- White background with border
- Use in sidebar layouts

### Loading Overlay
```html
<div class="gc-loading-overlay">
    <div class="gc-loading-spinner"></div>
</div>
```
- Full-screen overlay
- Backdrop blur effect
- Centered spinner animation

## ?? Responsive Breakpoints

### Mobile (<768px)
- Stacked header components
- Hamburger navigation menu
- Reduced padding and font sizes
- Static filter panels (not sticky)
- Vertical footer layout

### Tablet (768px - 1023px)
- Condensed navigation
- Medium padding
- Two-column layouts where appropriate

### Desktop (?1024px)
- Full horizontal navigation
- Maximum padding and spacing
- Multi-column layouts
- Sticky filter panels

## ? Accessibility Features

### Focus States
- 2px outline with offset
- Blue color (#335075)
- Applied to all interactive elements

### Screen Reader Support
```html
<span class="sr-only">Screen reader only text</span>
```

### ARIA Labels
- Navigation: `aria-label="Toggle navigation"`
- Breadcrumbs: `aria-label="breadcrumb"`
- Active page: `aria-current="page"`

### Keyboard Navigation
- Tab order follows logical flow
- All interactive elements focusable
- Focus visible on all controls

## ??? Print Styles

Optimized print layout:
- Hides: Navigation, header, footer, filters
- White background
- No shadows or hover effects
- Link URLs displayed in parentheses
- Page break controls for cards

## ?? Language Toggle

### Current Implementation
```javascript
function toggleLanguage() {
    const currentLang = document.documentElement.lang;
    const newLang = currentLang === 'en' ? 'fr' : 'en';
    // Updates button text
    // Future: Full i18n implementation
}
```

### Future Enhancement
- Integration with ASP.NET Core localization
- Resource files (.resx) for strings
- Culture-specific routing
- Persistent language preference

## ?? External Dependencies

### CDN Resources
```html
<!-- Bootstrap 5.3.3 -->
<link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css">

<!-- Bootstrap Icons 1.11.3 -->
<link href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css">

<!-- FontAwesome 6.4.2 -->
<link href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.2/css/all.min.css">

<!-- jQuery 3.7.1 -->
<script src="https://code.jquery.com/jquery-3.7.1.min.js"></script>

<!-- Bootstrap JS 5.3.3 -->
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js"></script>

<!-- SignalR (for real-time features) -->
<script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@latest/dist/browser/signalr.min.js"></script>
```

## ?? Customization Guide

### Changing Colors
Edit CSS variables in `portal.css`:
```css
:root {
    --gc-primary-blue: #YourColor;
    --gc-secondary-blue: #YourColor;
    /* ... */
}
```

### Adding New Components
Follow the naming convention:
- Prefix: `gc-` (Government of Canada)
- BEM-style modifiers: `gc-card--large`
- State classes: `gc-card.active`

### Custom Animations
All transitions use: `transition: all 0.2s ease;`
Override with specific timing if needed.

## ?? Testing Checklist

- [ ] Test in Chrome, Firefox, Edge, Safari
- [ ] Verify responsive breakpoints (mobile, tablet, desktop)
- [ ] Test keyboard navigation
- [ ] Verify screen reader compatibility
- [ ] Print page and verify layout
- [ ] Test with reduced motion preferences
- [ ] Verify color contrast ratios (WCAG AA)
- [ ] Test language toggle functionality

## ?? Usage Examples

### Create a GC-Styled Page
```razor
@{
    ViewData["Title"] = "Page Title";
}

<!-- Breadcrumb -->
<nav aria-label="breadcrumb" class="gc-breadcrumb">
    <ol class="breadcrumb">
        <li class="breadcrumb-item"><a href="/">Home</a></li>
        <li class="breadcrumb-item active">Current Page</li>
    </ol>
</nav>

<!-- Page Heading -->
<h1 class="gc-page-heading">
    <i class="fas fa-icon"></i> Page Title
</h1>

<!-- Content -->
<div class="row">
    <div class="col-lg-8">
        <div class="gc-card">
            <div class="gc-card-header">Card Title</div>
            <div class="gc-card-body">
                <p>Card content...</p>
            </div>
        </div>
    </div>
    
    <div class="col-lg-4">
        <div class="gc-filter-panel">
            <h3 class="gc-section-heading">Filters</h3>
            <!-- Filter controls -->
        </div>
    </div>
</div>
```

### Create a GC-Styled Form
```razor
<form method="post">
    <div class="gc-form-group">
        <label class="gc-form-label" for="name">Name</label>
        <input type="text" class="gc-form-control" id="name" name="name">
    </div>
    
    <div class="gc-form-group">
        <label class="gc-form-label" for="email">Email</label>
        <input type="email" class="gc-form-control" id="email" name="email">
    </div>
    
    <button type="submit" class="gc-btn-primary">
        <i class="fas fa-check"></i> Submit
    </button>
    <button type="reset" class="gc-btn-secondary">Reset</button>
</form>
```

## ?? Browser Support

- **Chrome**: Latest 2 versions ?
- **Firefox**: Latest 2 versions ?
- **Safari**: Latest 2 versions ?
- **Edge**: Latest 2 versions ?
- **Internet Explorer**: Not supported ?

## ?? License Notes

**Important**: The placeholder SVG files (`sig-blk-en.svg`, `wmms-blk.svg`) are simplified representations. 

For production use, obtain official Government of Canada brand assets from:
- **Canada.ca Design System**: https://design.canada.ca/
- **FIP Visual Identity**: Federal Identity Program guidelines

## ?? Next Steps

1. **Replace placeholder logos** with official GC assets
2. **Implement full i18n** with ASP.NET Core localization
3. **Add dark mode** support (prefers-color-scheme)
4. **Enhance accessibility** testing and ARIA landmarks
5. **Create component library** documentation
6. **Add unit tests** for JavaScript functions
7. **Performance optimization** (lazy loading, compression)

## ?? Support

For questions about the GC Design System implementation:
- Review this README
- Check `portal.css` for component styles
- Examine `_Layout.cshtml` for structure
- Test components in `Home/Index.cshtml`

---

**Version**: 1.0.0  
**Last Updated**: November 2025  
**Framework**: ASP.NET Core 8.0 (Razor Pages)
