# GC Design System - Quick Reference Guide

## ?? Color Variables

```css
--gc-primary-blue: #26374a      /* Headers, navigation, primary buttons */
--gc-secondary-blue: #335075    /* Hover states, accents */
--gc-background: #f8fafc        /* Main content background */
--gc-white: #ffffff             /* Cards, panels */
--gc-border-light: #e5e7eb      /* Borders */
```

## ?? Common Components

### Card
```html
<div class="gc-card">
    <div class="gc-card-header">Title</div>
    <div class="gc-card-body">Content</div>
</div>
```

### Buttons
```html
<button class="gc-btn-primary">Primary</button>
<button class="gc-btn-secondary">Secondary</button>
```

### Form
```html
<div class="gc-form-group">
    <label class="gc-form-label">Label</label>
    <input class="gc-form-control" type="text">
</div>
```

### Table
```html
<table class="gc-table">
    <thead><tr><th>Header</th></tr></thead>
    <tbody><tr><td>Data</td></tr></tbody>
</table>
```

### Badge
```html
<span class="gc-badge">Label</span>
```

### Breadcrumb
```html
<nav aria-label="breadcrumb" class="gc-breadcrumb">
    <ol class="breadcrumb">
        <li class="breadcrumb-item"><a href="#">Home</a></li>
        <li class="breadcrumb-item active">Current</li>
    </ol>
</nav>
```

### Headings
```html
<h1 class="gc-page-heading">Page Title</h1>
<h2 class="gc-section-heading">Section Title</h2>
```

## ?? Responsive Breakpoints

- **Mobile**: < 768px
- **Tablet**: 768px - 1023px  
- **Desktop**: ? 1024px

## ?? Page Template

```razor
@{
    ViewData["Title"] = "Page Title - ILRS Portal";
}

<!-- Breadcrumb -->
<nav aria-label="breadcrumb" class="gc-breadcrumb">
    <ol class="breadcrumb">
        <li class="breadcrumb-item">
            <a asp-controller="Home" asp-action="Index">
                <i class="fas fa-home"></i> Home
            </a>
        </li>
        <li class="breadcrumb-item active">Current Page</li>
    </ol>
</nav>

<!-- Page Heading -->
<h1 class="gc-page-heading">
    <i class="fas fa-icon"></i> Page Title
</h1>

<!-- Main Content -->
<div class="row">
    <div class="col-lg-8">
        <div class="gc-card">
            <div class="gc-card-header">Section Title</div>
            <div class="gc-card-body">
                <!-- Content -->
            </div>
        </div>
    </div>
    
    <div class="col-lg-4">
        <div class="gc-filter-panel">
            <h3 class="gc-section-heading">Sidebar</h3>
            <!-- Sidebar content -->
        </div>
    </div>
</div>
```

## ?? Icon Libraries

### FontAwesome (Recommended)
```html
<i class="fas fa-home"></i>
<i class="fas fa-user"></i>
<i class="fas fa-cog"></i>
```

### Bootstrap Icons
```html
<i class="bi bi-house"></i>
<i class="bi bi-person"></i>
<i class="bi bi-gear"></i>
```

## ? Accessibility

- Always use `aria-label` on navigation toggles
- Use `aria-current="page"` on active breadcrumb items
- Include `.sr-only` text for screen readers
- All focus states have visible outlines

## ?? Files Created/Modified

### New Files
- `wwwroot/css/portal.css` - GC Design System styles
- `wwwroot/images/sig-blk-en.svg` - GC Canada logo
- `wwwroot/images/wmms-blk.svg` - Canada wordmark
- `Views/Home/Components.cshtml` - Component showcase
- `GC-DESIGN-SYSTEM-README.md` - Full documentation
- `GC-QUICK-REFERENCE.md` - This file

### Modified Files
- `Views/Shared/_Layout.cshtml` - GC header, nav, footer
- `Views/Home/Index.cshtml` - GC-styled homepage
- `Views/Home/Privacy.cshtml` - Privacy page with GC components
- `wwwroot/css/site.css` - GC theme overrides
- `Controllers/HomeController.cs` - Added Components action

## ?? Getting Started

1. **View the homepage**: Navigate to `/` to see the main page
2. **Explore components**: Visit `/Home/Components` for full showcase
3. **Read documentation**: Check `GC-DESIGN-SYSTEM-README.md`
4. **Customize colors**: Edit CSS variables in `portal.css`

## ?? Resources

- [Canada.ca Design System](https://design.canada.ca/)
- [Bootstrap 5.3 Docs](https://getbootstrap.com/docs/5.3/)
- [FontAwesome Icons](https://fontawesome.com/icons)
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)

## ?? Tips

1. **Use semantic HTML**: Proper heading hierarchy (h1 ? h2 ? h3)
2. **Mobile-first**: Design for mobile, enhance for desktop
3. **Accessibility**: Test with keyboard navigation and screen readers
4. **Consistency**: Stick to GC design patterns across all pages
5. **Performance**: Minimize custom CSS, leverage existing classes

---

**Quick Demo**: Visit `/Home/Components` to see all components in action!
