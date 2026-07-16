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
    getElementWidth: (el) => {
        try { return el?.getBoundingClientRect().width ?? 0; }
        catch { return 0; }
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
    prefersReducedMotion: () => {
        try { return matchMedia('(prefers-reduced-motion: reduce)').matches; }
        catch { return false; }
    },
    // Confeti/fuegos del Resumen final (canvas-confetti self-hosted, global `confetti`)
    finalCeremony: {
        _rain: null,
        // Explosión al revelar al campeón: fireworks + cañones laterales (~2.2s)
        burst: () => {
            if (typeof confetti !== 'function') return;
            if (window.quiniela.prefersReducedMotion()) return;
            const colors = ['#F59E0B', '#FCD34D', '#3B82F6', '#10B981', '#FFFFFF'];
            const end = Date.now() + 2200;
            (function cannons() {
                confetti({ particleCount: 3, angle: 60, spread: 55, startVelocity: 55, colors, origin: { x: 0, y: 0.75 } });
                confetti({ particleCount: 3, angle: 120, spread: 55, startVelocity: 55, colors, origin: { x: 1, y: 0.75 } });
                if (Date.now() < end) requestAnimationFrame(cannons);
            })();
            [0.5, 0.25, 0.75].forEach((x, i) => setTimeout(() => confetti({
                particleCount: 110, spread: 110, startVelocity: 42, ticks: 220, scalar: 1.1, colors,
                origin: { x, y: 0.3 }
            }), i * 400));
        },
        // Caída suave continua de fondo, baja densidad; pausada con document.hidden
        startRain: (canvas) => {
            const fc = window.quiniela.finalCeremony;
            fc.stopRain();
            if (typeof confetti !== 'function' || !canvas) return;
            if (window.quiniela.prefersReducedMotion()) return;
            const inst = confetti.create(canvas, { resize: true });
            let last = 0, raf = 0;
            const loop = (now) => {
                raf = requestAnimationFrame(loop);
                if (document.hidden || now - last < 350) return;
                last = now;
                inst({
                    particleCount: 2, startVelocity: 4, gravity: 0.4, spread: 70,
                    ticks: 350, scalar: 0.8, drift: Math.random() - 0.5,
                    origin: { x: Math.random(), y: -0.1 }
                });
            };
            raf = requestAnimationFrame(loop);
            fc._rain = { stop: () => { cancelAnimationFrame(raf); inst.reset(); } };
        },
        stopRain: () => {
            const fc = window.quiniela.finalCeremony;
            if (fc._rain) { fc._rain.stop(); fc._rain = null; }
        }
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
