# Navbar Refactor — CommonUnderstanding v2
## Coding Agent Instructions

**File:** `Views/Shared/_Layout.cshtml`  
**Goal:** Reduce 10 top-level nav items to 4 visible items. Make the social Feed the default landing experience. Promote Submit to a primary CTA button. Collapse all private-workspace tools into a single dropdown.

---

## 1. Default Route Change

**File:** `Program.cs` (or `Startup.cs`)

Change the default MVC route so authenticated users land on the Feed instead of the Dashboard:

```csharp
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=SocialView}/{action=Feed}/{id?}");
```

> If you have a separate redirect in `HomeController` or `AccountController` post-login, update that as well to redirect to `SocialView/Feed`.

---

## 2. Replace the Left Nav `<ul>` Block

**In `_Layout.cshtml`**, locate the opening tag:

```html
<ul class="navbar-nav me-auto">
```

Replace the entire `<ul class="navbar-nav me-auto">...</ul>` block (everything up to the `<!-- Identity nav (right side) -->` comment) with the following:

```html
<ul class="navbar-nav me-auto align-items-center gap-1">

    <!-- PUBLIC: Feed -->
    <li class="nav-item">
        <a class="nav-link fw-semibold @(ViewContext.RouteData.Values["controller"]?.ToString() == "SocialView" && ViewContext.RouteData.Values["action"]?.ToString() == "Feed" ? "active" : "")"
           asp-controller="SocialView" asp-action="Feed">
            <i class="bi bi-fire"></i> Feed
        </a>
    </li>

    <!-- PUBLIC: Submit CTA (styled as button, not nav-link) -->
    <li class="nav-item">
        <a class="btn btn-sm btn-primary ms-1 me-2"
           asp-controller="Argument" asp-action="Submit">
            <i class="bi bi-plus-lg me-1"></i>Submit
        </a>
    </li>

    <!-- PRIVATE: My Workspace dropdown -->
    <li class="nav-item dropdown">
        <a class="nav-link dropdown-toggle @(new[]{"Argument","CommonUnderstanding","EmergentConclusions","Reference","Connections","Convergence","CollaborativeSession","Discovery"}.Contains(ViewContext.RouteData.Values["controller"]?.ToString()) ? "active" : "")"
           href="#" role="button" data-bs-toggle="dropdown" aria-expanded="false">
            <i class="bi bi-person-workspace"></i> My Workspace
        </a>
        <ul class="dropdown-menu">
            <li><h6 class="dropdown-header small text-uppercase" style="font-size:0.65rem; letter-spacing:0.06em;">Analysis</h6></li>
            <li>
                <a class="dropdown-item" asp-controller="Argument" asp-action="Index">
                    <i class="bi bi-diagram-3 me-2"></i>Arguments
                </a>
            </li>
            <li>
                <a class="dropdown-item" asp-controller="CommonUnderstanding" asp-action="Index">
                    <i class="bi bi-diagram-2 me-2"></i>Common Understanding
                </a>
            </li>
            <li>
                <a class="dropdown-item" asp-controller="EmergentConclusions" asp-action="Index">
                    <i class="bi bi-lightbulb me-2"></i>Emergent Conclusions
                </a>
            </li>
            <li>
                <a class="dropdown-item" asp-controller="Convergence" asp-action="Index">
                    <i class="bi bi-intersect me-2"></i>Convergence
                </a>
            </li>
            <li><hr class="dropdown-divider"></li>
            <li><h6 class="dropdown-header small text-uppercase" style="font-size:0.65rem; letter-spacing:0.06em;">People</h6></li>
            <li>
                <a class="dropdown-item" asp-controller="Discovery" asp-action="Profile">
                    <i class="bi bi-person me-2"></i>Belief Profile
                </a>
            </li>
            <li>
                <a class="dropdown-item" asp-controller="Connections" asp-action="Index">
                    <i class="bi bi-people me-2"></i>Connections
                </a>
            </li>
            <li>
                <a class="dropdown-item" asp-controller="CollaborativeSession" asp-action="Index">
                    <i class="bi bi-diagram-3-fill me-2"></i>Sessions
                </a>
            </li>
            <li><hr class="dropdown-divider"></li>
            <li>
                <a class="dropdown-item" asp-controller="Reference" asp-action="Index">
                    <i class="bi bi-book me-2"></i>Reference
                </a>
            </li>
        </ul>
    </li>

    <!-- SETTINGS (icon-only with AI status dot) -->
    <li class="nav-item">
        <a class="nav-link @(ViewContext.RouteData.Values["controller"]?.ToString() == "Settings" ? "active" : "")"
           asp-controller="Settings" asp-action="Index" title="Settings">
            <i class="bi bi-gear"></i>
            <span id="navAiStatusDot"
                  style="display:inline-block;width:7px;height:7px;border-radius:50%;background:#6b7280;margin-left:2px;vertical-align:middle;"></span>
        </a>
    </li>

</ul>
```

### Items removed from top nav (intentional)
- `Dashboard` — replaced by Feed as the default landing page
- `Chain Builder`, `Worldview Composer`, `Debate Room` — removed from nav; surface these contextually within Feed/Submit flows when they are fully implemented

---

## 3. Fix the AI Status Dot JavaScript

**In `_Layout.cshtml`**, locate this script block:

```javascript
(async function updateNavAiDot() {
    try {
        const r = await fetch('/api/AIStatus/status');
        const d = await r.json();
        const dot = document.getElementById('navAiStatusDot');
        if (!dot) return;
        if (d.ollamaConnected && d.modelLoaded) dot.style.background = '#198754';
        else if (d.ollamaConnected)              dot.style.background = '#ffc107';
        else                                     dot.style.background = '#dc3545';
        dot.title = d.ollamaConnected ? (d.modelLoaded ? 'AI: Ready' : 'AI: Partial') : 'AI: Offline';
    } catch { /* silent fail */ }
    setTimeout(updateNavAiDot, 30000);
})();
```

Replace with:

```javascript
(async function updateNavAiDot() {
    try {
        const r = await fetch('/api/AIStatus/status');
        const d = await r.json();
        const dot = document.getElementById('navAiStatusDot');
        if (!dot) return;
        // Supports both Ollama-era and Azure Foundry-era response shapes
        const ready   = d.isReady   ?? d.modelLoaded      ?? d.ollamaConnected ?? false;
        const partial = d.isPartial ?? d.ollamaConnected  ?? false;
        if (ready)        { dot.style.background = '#198754'; dot.title = 'AI: Ready'; }
        else if (partial) { dot.style.background = '#ffc107'; dot.title = 'AI: Partial'; }
        else              { dot.style.background = '#dc3545'; dot.title = 'AI: Offline'; }
    } catch { /* silent fail */ }
    setTimeout(updateNavAiDot, 30_000);
})();
```

> **Note for agent:** Also update `AIStatusController` (or the `/api/AIStatus/status` endpoint) to return `isReady: bool` and optionally `isPartial: bool` reflecting Azure AI Foundry + DeepSeek connectivity, rather than `ollamaConnected`/`modelLoaded`.

---

## 4. Fix the Language Toggle

**In `_Layout.cshtml`**, locate the `toggleLanguage()` function and replace with a no-op stub:

```javascript
function toggleLanguage() {
    // i18n not yet implemented — stub to prevent alert() in production
    console.info('[i18n] Language toggle not yet implemented.');
}
```

> Remove the `alert()` call. It is not appropriate for a production interface.

---

## 5. Remove the Dashboard Nav Item Active-State JS

The existing JS at the bottom of `_Layout.cshtml` attempts to set active states by matching `href` against `window.location.pathname`:

```javascript
document.addEventListener('DOMContentLoaded', function() {
    const currentPath = window.location.pathname;
    document.querySelectorAll('.cu-nav .nav-link').forEach(link => {
        if (link.getAttribute('href') === currentPath) {
            link.classList.add('active');
        }
    });
});
```

This is now redundant — active states are handled by the Razor `@(... ? "active" : "")` expressions inline on each `<a>` tag. **Remove this block entirely** to avoid double-application of the `active` class.

---

## 6. Summary of Visible Nav Items After Changes

| Position | Element | Type | Destination |
|---|---|---|---|
| Left 1 | **Feed** | `nav-link fw-semibold` | `SocialView/Feed` |
| Left 2 | **Submit** | `btn btn-sm btn-primary` | `Argument/Submit` |
| Left 3 | **My Workspace ▾** | dropdown | All private tools (grouped) |
| Left 4 | **⚙** (icon) | `nav-link` | `Settings/Index` |
| Right | **User ▾** | dropdown | Profile, Connections, Sign Out |

**Before:** 10 top-level nav items  
**After:** 4 visible items + user identity dropdown (unchanged)

---

## 7. No Changes Required

The following sections of `_Layout.cshtml` require **no modifications**:

- `<header>` block (logo, site title, FR language button)
- Identity nav (right side — user dropdown, sign-in/register links)
- `<footer>` block
- SignalR script include
- AI fetch tracing script (`attachAiFetchTracing`)
- Bootstrap and jQuery CDN includes

