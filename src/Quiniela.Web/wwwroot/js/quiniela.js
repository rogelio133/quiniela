window.getClientTimezone = () => {
    try { return Intl.DateTimeFormat().resolvedOptions().timeZone; }
    catch { return 'UTC'; }
};

if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        navigator.serviceWorker.register('/service-worker.js');
    });
}

// Lazy-load Three.js login scene only when the login canvas is present
async function tryInitLoginScene() {
    if (!document.getElementById('login-canvas')) return;
    try {
        const mod = await import('/js/three-login.js');
        if (mod.initScene) mod.initScene();
    } catch (e) {
        console.warn('Three.js login scene unavailable:', e);
    }
}

document.addEventListener('DOMContentLoaded', tryInitLoginScene);
document.addEventListener('blazor:navigated', tryInitLoginScene);

// Lazy wrappers for Blazor IJSRuntime → Three.js modules
window.quiniela = {
    initTrophy: async (id) => {
        try { const m = await import('/js/three-trophy.js'); m.initTrophy(id); }
        catch (e) { console.warn('Trophy 3D unavailable:', e); }
    },
    disposeTrophy: async (id) => {
        try { const m = await import('/js/three-trophy.js'); m.disposeTrophy(id); }
        catch { }
    },
    initStadium: async (id) => {
        try { const m = await import('/js/three-stadium.js'); m.initStadium(id); }
        catch (e) { console.warn('Stadium 3D unavailable:', e); }
    },
    disposeStadium: async () => {
        try { const m = await import('/js/three-stadium.js'); m.disposeStadium(); }
        catch { }
    },
    initBall: async (id) => {
        try { const m = await import('/js/three-ball.js'); m.initBall(id); }
        catch (e) { console.warn('Ball 3D unavailable:', e); }
    },
    disposeBall: async (id) => {
        try { const m = await import('/js/three-ball.js'); m.disposeBall(id); }
        catch { }
    },
    countUp: (elementId, endValue, durationMs) => {
        const el = document.getElementById(elementId);
        if (!el) return;
        const start = performance.now();
        function frame(now) {
            const t = Math.min((now - start) / durationMs, 1);
            const eased = 1 - Math.pow(1 - t, 3);
            el.textContent = Math.round(eased * endValue).toString();
            if (t < 1) requestAnimationFrame(frame);
        }
        requestAnimationFrame(frame);
    },
};
