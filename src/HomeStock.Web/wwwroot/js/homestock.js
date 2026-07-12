// Small, framework-agnostic JS helpers invoked from Blazor via IJSRuntime.
window.homestock = {
    // Theme: 'auto' | 'light' | 'dark'. Persisted in localStorage and applied to <html>.
    getTheme: function () {
        return localStorage.getItem('homestock-theme') || 'auto';
    },
    setTheme: function (theme) {
        localStorage.setItem('homestock-theme', theme);
        var resolved = theme === 'auto'
            ? (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
            : theme;
        document.documentElement.setAttribute('data-bs-theme', resolved);
        return resolved;
    },
    // Focus a field by id (used to speed up quick data entry).
    focus: function (id) {
        var el = document.getElementById(id);
        if (el) el.focus();
    }
};

// ---- PWA install prompt handling ----
// Capture the browser's install prompt so the app can offer an "Install" button on demand.
window.homestockPwa = (function () {
    let deferredPrompt = null;
    window.addEventListener('beforeinstallprompt', function (e) {
        e.preventDefault();
        deferredPrompt = e;
    });
    window.addEventListener('appinstalled', function () { deferredPrompt = null; });
    return {
        canInstall: function () { return deferredPrompt !== null; },
        // Returns 'accepted', 'dismissed', or 'unavailable'.
        promptInstall: async function () {
            if (!deferredPrompt) return 'unavailable';
            deferredPrompt.prompt();
            const choice = await deferredPrompt.userChoice;
            deferredPrompt = null;
            return choice && choice.outcome ? choice.outcome : 'dismissed';
        },
        isStandalone: function () {
            return window.matchMedia('(display-mode: standalone)').matches ||
                window.navigator.standalone === true;
        }
    };
})();

// Register the offline app-shell service worker (progressive enhancement).
if ('serviceWorker' in navigator) {
    window.addEventListener('load', function () {
        navigator.serviceWorker.register('service-worker.js').catch(function () { /* offline shell optional */ });
    });
}
