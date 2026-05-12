/* ========================================
   GC Design System - Interactive Components
   ILRS Portal JavaScript
   ======================================== */

// Language Toggle with Persistent State
function toggleLanguage() {
    const currentLang = document.documentElement.lang || 'en';
    const newLang = currentLang === 'en' ? 'fr' : 'en';
    
    // Update HTML lang attribute
    document.documentElement.lang = newLang;
    
    // Update button text
    const langBtn = document.querySelector('.gc-lang-toggle');
    if (langBtn) {
        langBtn.innerHTML = newLang === 'en' 
            ? '<i class="fas fa-globe"></i> FR' 
            : '<i class="fas fa-globe"></i> EN';
        langBtn.setAttribute('title', newLang === 'en' ? 'Français' : 'English');
    }
    
    // Store preference in localStorage
    localStorage.setItem('preferred-language', newLang);
    
    // Log for debugging
    console.log(`Language switched to: ${newLang}`);
    
    // Show temporary notification
    showNotification(`Language preference saved: ${newLang.toUpperCase()}`, 'info');
}

// Show Notification Toast
function showNotification(message, type = 'info') {
    // Remove existing notification if present
    const existing = document.querySelector('.gc-notification');
    if (existing) {
        existing.remove();
    }
    
    // Create notification element
    const notification = document.createElement('div');
    notification.className = `gc-notification alert alert-${type}`;
    notification.style.cssText = `
        position: fixed;
        top: 100px;
        right: 20px;
        z-index: 9999;
        min-width: 300px;
        box-shadow: 0 4px 12px rgba(38, 55, 74, 0.2);
        animation: slideInRight 0.3s ease-out;
    `;
    
    notification.innerHTML = `
        <div class="d-flex align-items-center justify-content-between">
            <span>${message}</span>
            <button type="button" class="btn-close" onclick="this.parentElement.parentElement.remove()"></button>
        </div>
    `;
    
    document.body.appendChild(notification);
    
    // Auto-remove after 3 seconds
    setTimeout(() => {
        if (notification.parentElement) {
            notification.style.animation = 'slideOutRight 0.3s ease-out';
            setTimeout(() => notification.remove(), 300);
        }
    }, 3000);
}

// Initialize Language Preference on Page Load
document.addEventListener('DOMContentLoaded', function() {
    // Restore language preference
    const savedLang = localStorage.getItem('preferred-language');
    if (savedLang && savedLang !== document.documentElement.lang) {
        document.documentElement.lang = savedLang;
        const langBtn = document.querySelector('.gc-lang-toggle');
        if (langBtn) {
            langBtn.innerHTML = savedLang === 'en' 
                ? '<i class="fas fa-globe"></i> FR' 
                : '<i class="fas fa-globe"></i> EN';
        }
    }
    
    // Highlight active navigation item
    highlightActiveNav();
    
    // Initialize collapsible sections
    initCollapsibleSections();
    
    // Add smooth scroll behavior to anchor links
    initSmoothScroll();
    
    // Initialize tooltips if Bootstrap is available
    if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }
    
    console.log('GC Design System initialized');
});

// Highlight Active Navigation Item
function highlightActiveNav() {
    const currentPath = window.location.pathname.toLowerCase();
    const navLinks = document.querySelectorAll('.gc-nav .nav-link');
    
    navLinks.forEach(link => {
        const href = link.getAttribute('href');
        if (href && currentPath.includes(href.toLowerCase()) && href !== '/') {
            link.classList.add('active');
        } else if (href === '/' && currentPath === '/') {
            link.classList.add('active');
        }
    });
}

// Initialize Collapsible Sections
function initCollapsibleSections() {
    const collapsibleHeaders = document.querySelectorAll('.gc-collapsible-header');
    
    collapsibleHeaders.forEach(header => {
        // Add click event if not using Bootstrap collapse
        if (!header.hasAttribute('data-bs-toggle')) {
            header.addEventListener('click', function() {
                this.classList.toggle('collapsed');
                const content = this.nextElementSibling;
                if (content && content.classList.contains('collapse')) {
                    content.classList.toggle('show');
                }
            });
        }
        
        // Sync collapsed state with Bootstrap collapse
        const targetId = header.getAttribute('data-bs-target');
        if (targetId) {
            const target = document.querySelector(targetId);
            if (target) {
                target.addEventListener('shown.bs.collapse', function() {
                    header.classList.remove('collapsed');
                });
                target.addEventListener('hidden.bs.collapse', function() {
                    header.classList.add('collapsed');
                });
            }
        }
    });
}

// Smooth Scroll for Anchor Links
function initSmoothScroll() {
    const anchorLinks = document.querySelectorAll('a[href^="#"]:not([href="#"])');
    
    anchorLinks.forEach(link => {
        link.addEventListener('click', function(e) {
            const targetId = this.getAttribute('href').substring(1);
            const targetElement = document.getElementById(targetId);
            
            if (targetElement) {
                e.preventDefault();
                targetElement.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
                
                // Update URL without jumping
                if (history.pushState) {
                    history.pushState(null, null, '#' + targetId);
                }
            }
        });
    });
}

// Show Loading Overlay
function showLoadingOverlay(message = 'Loading...') {
    // Remove existing overlay if present
    hideLoadingOverlay();
    
    const overlay = document.createElement('div');
    overlay.className = 'gc-loading-overlay';
    overlay.id = 'gcLoadingOverlay';
    overlay.innerHTML = `
        <div class="text-center">
            <div class="gc-loading-spinner"></div>
            <p class="text-white mt-3">${message}</p>
        </div>
    `;
    
    document.body.appendChild(overlay);
    document.body.style.overflow = 'hidden';
}

// Hide Loading Overlay
function hideLoadingOverlay() {
    const overlay = document.getElementById('gcLoadingOverlay');
    if (overlay) {
        overlay.remove();
        document.body.style.overflow = '';
    }
}

// Form Validation Helper
function validateGCForm(formId) {
    const form = document.getElementById(formId);
    if (!form) return false;
    
    let isValid = true;
    const requiredFields = form.querySelectorAll('[required]');
    
    requiredFields.forEach(field => {
        if (!field.value.trim()) {
            isValid = false;
            field.classList.add('is-invalid');
            
            // Add error message if not present
            if (!field.nextElementSibling || !field.nextElementSibling.classList.contains('invalid-feedback')) {
                const errorMsg = document.createElement('div');
                errorMsg.className = 'invalid-feedback';
                errorMsg.textContent = 'This field is required.';
                field.parentNode.insertBefore(errorMsg, field.nextSibling);
            }
        } else {
            field.classList.remove('is-invalid');
        }
    });
    
    if (!isValid) {
        showNotification('Please fill in all required fields.', 'danger');
    }
    
    return isValid;
}

// Remove Badge (for removable badges)
function removeBadge(badgeElement) {
    if (badgeElement && badgeElement.classList.contains('gc-badge-removable')) {
        badgeElement.style.animation = 'fadeOut 0.2s ease-out';
        setTimeout(() => badgeElement.remove(), 200);
    }
}

// Export User Data (Demo)
function exportUserData() {
    showLoadingOverlay('Preparing your data...');
    
    setTimeout(() => {
        const data = {
            exportDate: new Date().toISOString(),
            userProfile: {
                // This would be populated from actual user data
                preferences: localStorage.getItem('preferred-language') || 'en'
            }
        };
        
        const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'ilrs-user-data.json';
        a.click();
        URL.revokeObjectURL(url);
        
        hideLoadingOverlay();
        showNotification('Your data has been exported successfully!', 'success');
    }, 1500);
}

// Print Page with Custom Styles
function printPage() {
    window.print();
}

// Back to Top Button
function addBackToTop() {
    // Create button if it doesn't exist
    let backToTopBtn = document.getElementById('backToTopBtn');
    
    if (!backToTopBtn) {
        backToTopBtn = document.createElement('button');
        backToTopBtn.id = 'backToTopBtn';
        backToTopBtn.className = 'gc-btn-primary';
        backToTopBtn.innerHTML = '<i class="fas fa-arrow-up"></i>';
        backToTopBtn.setAttribute('aria-label', 'Back to top');
        backToTopBtn.style.cssText = `
            position: fixed;
            bottom: 20px;
            right: 20px;
            z-index: 1000;
            display: none;
            width: 50px;
            height: 50px;
            border-radius: 50%;
            padding: 0;
            box-shadow: 0 4px 12px rgba(38, 55, 74, 0.3);
        `;
        
        backToTopBtn.addEventListener('click', function() {
            window.scrollTo({
                top: 0,
                behavior: 'smooth'
            });
        });
        
        document.body.appendChild(backToTopBtn);
    }
    
    // Show/hide based on scroll position
    window.addEventListener('scroll', function() {
        if (window.pageYOffset > 300) {
            backToTopBtn.style.display = 'block';
        } else {
            backToTopBtn.style.display = 'none';
        }
    });
}

// Initialize back to top on load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', addBackToTop);
} else {
    addBackToTop();
}

// Add CSS animations for notifications
const style = document.createElement('style');
style.textContent = `
    @keyframes slideInRight {
        from {
            transform: translateX(100%);
            opacity: 0;
        }
        to {
            transform: translateX(0);
            opacity: 1;
        }
    }
    
    @keyframes slideOutRight {
        from {
            transform: translateX(0);
            opacity: 1;
        }
        to {
            transform: translateX(100%);
            opacity: 0;
        }
    }
    
    @keyframes fadeOut {
        from { opacity: 1; }
        to { opacity: 0; }
    }
`;
document.head.appendChild(style);

// Expose functions globally for inline onclick handlers
window.toggleLanguage = toggleLanguage;
window.showNotification = showNotification;
window.showLoadingOverlay = showLoadingOverlay;
window.hideLoadingOverlay = hideLoadingOverlay;
window.validateGCForm = validateGCForm;
window.removeBadge = removeBadge;
window.exportUserData = exportUserData;
window.printPage = printPage;
