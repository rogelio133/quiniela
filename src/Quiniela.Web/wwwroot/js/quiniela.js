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
        if (el) window.quiniela._countUpEl(el, endValue, durationMs);
    },
    _countUpEl: (el, endValue, durationMs) => {
        const start = performance.now();
        function frame(now) {
            const t = Math.min((now - start) / durationMs, 1);
            const eased = 1 - Math.pow(1 - t, 3);
            el.textContent = Math.round(eased * endValue).toString();
            if (t < 1) requestAnimationFrame(frame);
        }
        requestAnimationFrame(frame);
    },
    // Scroll-reveals del Resumen final (RF3): un IntersectionObserver agrega
    // .fs-visible a cada .fs-reveal al entrar al viewport (una sola vez); todo el
    // movimiento es CSS. Los <span data-countup="N"> dentro del elemento revelado
    // animan su número al mismo tiempo. Idempotente: llamadas repetidas solo
    // observan nodos nuevos. Con prefers-reduced-motion todo aparece de inmediato.
    observeReveals: () => {
        const els = document.querySelectorAll('.fs-reveal:not(.fs-observed)');
        if (!els.length) return;
        const reduce = window.quiniela.prefersReducedMotion();
        const show = (el) => {
            el.classList.add('fs-visible');
            el.querySelectorAll('[data-countup]').forEach(n => {
                const end = Number(n.getAttribute('data-countup')) || 0;
                if (reduce) n.textContent = end.toString();
                else window.quiniela._countUpEl(n, end, 900);
            });
        };
        if (reduce || !('IntersectionObserver' in window)) {
            els.forEach(el => { el.classList.add('fs-observed'); show(el); });
            return;
        }
        const io = new IntersectionObserver(entries => {
            entries.forEach(e => {
                if (!e.isIntersecting) return;
                io.unobserve(e.target);
                show(e.target);
            });
        }, { threshold: 0.2 });
        els.forEach(el => { el.classList.add('fs-observed'); io.observe(el); });
    },
    prefersReducedMotion: () => {
        try { return matchMedia('(prefers-reduced-motion: reduce)').matches; }
        catch { return false; }
    },
    // Vitrina de insignias (Resumen final): mientras la sección está en
    // pantalla, el scroll vertical desplaza las tarjetas en horizontal.
    // Técnica: la sección se estira (alto = alto del sticky + recorrido
    // horizontal faltante), un contenedor sticky queda fijo y el track se
    // traslada en X según el avance del scroll — al agotar el recorrido el
    // sticky se despega y la página sigue en vertical, sin scroll-jacking.
    // Con prefers-reduced-motion no se activa (queda la pila vertical).
    vitrina: {
        _s: null,
        init: (section) => {
            const q = window.quiniela;
            q.vitrina.destroy();
            // Un ElementReference sin capturar llega como objeto plano
            // ({__internalId: null}), truthy pero sin métodos de DOM.
            if (!(section instanceof Element) || q.prefersReducedMotion()) return;
            const sticky = section.querySelector('.fs-vit-sticky');
            const track = section.querySelector('.fs-vit-track');
            if (!sticky || !track) return;

            section.classList.add('fs-vit-on');
            const s = { section, sticky, track, extra: 0, top: 0, raf: 0 };

            const update = () => {
                if (s.extra <= 0) return;
                const gone = s.top - section.getBoundingClientRect().top;
                const y = Math.min(Math.max(gone, 0), s.extra);
                track.style.transform = `translate3d(${-y}px,0,0)`;
            };
            const measure = () => {
                s.top = parseFloat(getComputedStyle(sticky).top) || 0;
                s.extra = Math.max(0, track.scrollWidth - track.clientWidth);
                // Sin recorrido (todo cabe a lo ancho) el hint de "avanza en
                // horizontal" mentiría — ocultarlo vía clase.
                section.classList.toggle('fs-vit-noscroll', s.extra <= 0);
                if (s.extra > 0) {
                    section.style.height = (sticky.offsetHeight + s.extra) + 'px';
                } else {
                    // Todo cabe a lo ancho: sin recorrido, la sección mide lo del sticky.
                    section.style.height = '';
                    track.style.transform = '';
                }
                update();
            };
            s.onScroll = () => {
                if (s.raf) return;
                s.raf = requestAnimationFrame(() => { s.raf = 0; update(); });
            };
            s.measure = measure;
            window.addEventListener('scroll', s.onScroll, { passive: true });
            window.addEventListener('resize', s.measure);
            if ('ResizeObserver' in window) {
                s.ro = new ResizeObserver(() => measure());
                s.ro.observe(track);
            }
            q.vitrina._s = s;
            measure();
        },
        destroy: () => {
            const s = window.quiniela.vitrina._s;
            if (!s) return;
            window.removeEventListener('scroll', s.onScroll);
            window.removeEventListener('resize', s.measure);
            if (s.ro) s.ro.disconnect();
            if (s.raf) cancelAnimationFrame(s.raf);
            s.section.classList.remove('fs-vit-on', 'fs-vit-noscroll');
            s.section.style.height = '';
            s.track.style.transform = '';
            window.quiniela.vitrina._s = null;
        }
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
            // canvas debe ser un elemento real: un ElementReference sin capturar
            // llega como objeto plano truthy y confetti truena en cada frame.
            if (typeof confetti !== 'function' || !(canvas instanceof HTMLCanvasElement)) return;
            if (window.quiniela.prefersReducedMotion()) return;
            const inst = confetti.create(canvas, { resize: true });
            let last = 0, raf = 0;
            const loop = (now) => {
                raf = requestAnimationFrame(loop);
                if (document.hidden || now - last < 350) return;
                last = now;
                try {
                    inst({
                        particleCount: 2, startVelocity: 4, gravity: 0.4, spread: 70,
                        ticks: 350, scalar: 0.8, drift: Math.random() - 0.5,
                        origin: { x: Math.random(), y: -0.1 }
                    });
                } catch {
                    cancelAnimationFrame(raf); // canvas inservible: cortar el loop, no spamear
                }
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
