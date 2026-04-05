/**
 * Monica Swagger UI - Custom Navigation Buttons
 * Injects configurable navigation buttons into Swagger UI topbar
 */
(function () {
    'use strict';

    // Namespace for Swagger customizations
    window.MoSwagger = window.MoSwagger || {};

    /**
     * Configuration object injected from C# (will be populated by HeadContent)
     * Format: { buttons: [{ name, path, description, order, enabled }] }
     */
    window.MoSwagger.config = window.MoSwagger.config || { buttons: [] };

    /**
     * Initialize navigation buttons when Swagger UI is ready
     */
    function initNavigationButtons() {
        const buttons = (window.MoSwagger.config.buttons || [])
            .filter(btn => btn.enabled !== false)
            .sort((a, b) => (a.order || 0) - (b.order || 0));

        if (buttons.length === 0) {
            return; // No buttons to inject
        }

        // Wait for Swagger UI topbar to be available
        waitForElement('.topbar', function(topbar) {
            injectButtonsIntoTopbar(topbar, buttons);
        });
    }

    /**
     * Wait for an element to appear in DOM
     * @param {string} selector - CSS selector
     * @param {function} callback - Callback when element is found
     * @param {number} maxAttempts - Maximum retry attempts
     */
    function waitForElement(selector, callback, maxAttempts = 50) {
        let attempts = 0;

        const interval = setInterval(function() {
            const element = document.querySelector(selector);

            if (element) {
                clearInterval(interval);
                callback(element);
            } else if (++attempts >= maxAttempts) {
                clearInterval(interval);
                console.warn('MoSwagger: Topbar element not found after', maxAttempts, 'attempts');
            }
        }, 100); // Check every 100ms
    }

    /**
     * Inject custom buttons into Swagger topbar
     * @param {HTMLElement} topbar - Swagger UI topbar element
     * @param {Array} buttons - Button configuration array
     */
    function injectButtonsIntoTopbar(topbar, buttons) {
        // Find the wrapper that contains topbar content
        const wrapper = topbar.querySelector('.topbar-wrapper') || topbar;

        // Create container for custom buttons
        const buttonContainer = document.createElement('div');
        buttonContainer.className = 'mo-swagger-nav-buttons';

        // Create each button
        buttons.forEach(function(btnConfig) {
            const button = createButton(btnConfig);
            buttonContainer.appendChild(button);
        });

        // Insert button container into topbar (right side)
        wrapper.appendChild(buttonContainer);
    }

    /**
     * Create a single navigation button
     * @param {object} config - Button configuration
     * @returns {HTMLElement} Button element
     */
    function createButton(config) {
        const button = document.createElement('a');
        button.className = 'mo-swagger-nav-btn';
        button.href = '#';
        button.setAttribute('data-path', config.path || '/');

        // Add tooltip if description exists
        if (config.description) {
            button.setAttribute('title', config.description);
            button.setAttribute('aria-label', config.description);
        }

        // Add button text
        const text = document.createElement('span');
        text.className = 'mo-btn-text';
        text.textContent = config.name || 'Link';
        button.appendChild(text);

        // Add click handler for navigation
        button.addEventListener('click', function(e) {
            e.preventDefault();
            const path = this.getAttribute('data-path');

            // Navigate to path (same tab)
            if (path) {
                // Handle both absolute and relative paths
                const targetUrl = path.startsWith('http')
                    ? path
                    : resolveRelativePath(path);
                window.location.href = targetUrl;
            }
        });

        return button;
    }

    /**
     * Resolve relative path to absolute URL
     * @param {string} path - Relative path (e.g., "system-info", "/system-info", "~/system-info")
     * @returns {string} Absolute URL
     */
    function resolveRelativePath(path) {
        // Remove leading ~ if present
        const cleanPath = path.replace(/^~\//, '');

        // Get base URL (protocol + host + port)
        const baseUrl = window.location.origin;

        // Ensure path starts with /
        const normalizedPath = cleanPath.startsWith('/') ? cleanPath : '/' + cleanPath;

        return baseUrl + normalizedPath;
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initNavigationButtons);
    } else {
        // DOM already loaded
        initNavigationButtons();
    }

})();
