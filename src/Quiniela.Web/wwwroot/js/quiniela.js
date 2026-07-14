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

// Al navegar, Blazor sincroniza los atributos de <html> con el documento del
// servidor (que no conoce el tema del cliente) y borra data-theme/data-bs-theme
// — sin disparar ningún evento blazor:* observable. Vigilar y re-aplicar.
new MutationObserver(() => {
    const d = document.documentElement;
    if (!d.getAttribute('data-theme') || !d.getAttribute('data-bs-theme'))
        window.quiniela.theme.apply();
}).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme', 'data-bs-theme'] });

// Lazy wrappers for Blazor IJSRuntime → Three.js modules
window.quiniela = {
    theme: {
        get: () => document.documentElement.getAttribute('data-theme'),
        set: (t) => {
            try { localStorage.setItem('quiniela_theme', t); } catch { }
            window.quiniela.theme.apply();
        },
        // Re-aplica el tema efectivo (guardado o sistema) a <html> y al meta theme-color.
        // La enhanced navigation de Blazor fusiona los atributos de <html> con la
        // respuesta del servidor (que no trae data-theme), así que hay que re-aplicar
        // tras cada navegación — no basta con el script del <head> (solo cold loads).
        apply: () => {
            let t;
            try { t = localStorage.getItem('quiniela_theme'); } catch { }
            if (t !== 'light' && t !== 'dark')
                t = matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
            const d = document.documentElement;
            d.setAttribute('data-theme', t);
            d.setAttribute('data-bs-theme', t);
            const m = document.querySelector('meta[name="theme-color"]');
            if (m) m.content = t === 'dark' ? '#0B1220' : '#0D1B2A';
        }
    },
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
    getStoredList: (key) => {
        try { return JSON.parse(localStorage.getItem(key) ?? '[]'); }
        catch { return []; }
    },
    setStoredList: (key, list) => {
        try { localStorage.setItem(key, JSON.stringify(list)); }
        catch { }
    },
    pushShouldPrompt: async () => {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) return false;
        if (Notification.permission === 'denied') return false;
        try {
            const dismissedAt = Number(localStorage.getItem('quiniela_push_dismissed'));
            if (dismissedAt) {
                const hours = (Date.now() - dismissedAt) / 3600000;
                if (hours < 6) return false;
            }
        } catch { }
        try {
            const reg = await navigator.serviceWorker.ready;
            const sub = await reg.pushManager.getSubscription();
            return sub === null;
        } catch { return false; }
    },
    pushMarkDismissed: () => {
        try { localStorage.setItem('quiniela_push_dismissed', Date.now().toString()); } catch { }
    },
    pushSubscribe: async (vapidPublicKey) => {
        try {
            if (!('serviceWorker' in navigator) || !('PushManager' in window)) return null;
            const permission = await Notification.requestPermission();
            if (permission !== 'granted') return null;

            const reg = await navigator.serviceWorker.ready;
            let sub = await reg.pushManager.getSubscription();
            if (!sub) {
                sub = await reg.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: urlBase64ToUint8Array(vapidPublicKey)
                });
            }
            const json = sub.toJSON();
            return { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth };
        } catch (e) {
            console.warn('Push subscribe failed:', e);
            return null;
        }
    },
};

function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    const rawData = atob(base64);
    return Uint8Array.from([...rawData].map(c => c.charCodeAt(0)));
}
