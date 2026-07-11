# Plan de Mejoras — Look & Feel + Animaciones Three.js
## Quiniela Mundial 2026

---

## 1. Bugs de Interfaz Detectados

### BUG-01 — Iconos faltantes en NavMenu (visual roto)
**Archivo:** `Components/Layout/NavMenu.razor` + `NavMenu.razor.css`

Los iconos de tres ítems del menú no están definidos en el CSS. La clase `.bi` muestra un cuadro vacío de 1.25rem × 1.25rem sin imagen:

| Ítem de menú | Clase usada | ¿Definida? |
|---|---|---|
| Inicio | `bi-house-door-fill-nav-menu` | ✅ |
| **Mis Salas** | `bi-grid-fill-nav-menu` | ❌ FALTA |
| Crear sala | `bi-plus-square-fill-nav-menu` | ✅ |
| Panel Admin | `bi-gear-fill-nav-menu` | ✅ |
| **Mi Perfil** | `bi-person-circle-nav-menu` | ❌ FALTA |
| **Cerrar sesión** | `bi-arrow-bar-left-nav-menu` | ❌ FALTA |

**Fix:** Agregar en `NavMenu.razor.css` las tres definiciones SVG faltantes.

---

### BUG-02 — Texto blanco sobre fondo gris claro en top-bar (contraste crítico)
**Archivo:** `Components/Layout/MainLayout.razor` + `MainLayout.razor.css`

```razor
<!-- MainLayout.razor -->
<span class="text-white small">@(context.User...)</span>
```

```css
/* MainLayout.razor.css */
.top-row {
    background-color: #f7f7f7;  /* gris casi blanco */
}
```

El nombre del usuario se renderiza en blanco (`#ffffff`) sobre fondo `#f7f7f7`. El contraste es prácticamente nulo — el texto es invisible. Esto viola WCAG 2.1 AA (ratio mínimo 4.5:1; este tiene ~1.03:1).

**Fix:** Cambiar el color del texto a `text-dark` o `text-muted`, o cambiar el fondo de la top-bar.

---

### BUG-03 — `lang="en"` en documento HTML en español
**Archivo:** `Components/App.razor`, línea 2

```html
<html lang="en">  ← debería ser lang="es"
```

Afecta lectores de pantalla, motores de búsqueda, corrección ortográfica del navegador y separación de sílabas CSS.

**Fix:** Cambiar a `<html lang="es">`.

---

### BUG-04 — Acordeón sin animación de colapso (Bootstrap JS no cargado)
**Archivos:** `Components/App.razor`, `Components/Pages/Predictions/Index.razor`

`App.razor` carga solo el CSS de Bootstrap; no incluye `bootstrap.bundle.min.js`. Los acordeones de Pronósticos y Mis Pronósticos cambian entre estados con snap inmediato (sin transición de altura). La flecha del botón `accordion-button` tampoco rota al colapsar.

**Fix — opción A (recomendada):** Añadir `<script src="lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>` en `App.razor` y refactorizar el toggle de Blazor para que invoque el API de Bootstrap JS en vez de mutar clases directamente.

**Fix — opción B:** Implementar la animación de colapso manualmente con CSS `max-height` y `transition`.

---

### BUG-05 — Dimming asimétrico al bloquear partidos
**Archivo:** `wwwroot/app.css`, línea 102

```css
.match-locked .card-body {
    opacity: 0.65;
}
```

Solo el `card-body` (área de selección) se oscurece al llegar el kickoff. El `card-header` (grupo, estadio, hora) y el `card-footer` (resultado/botón) quedan al 100% de opacidad. Visualmente inconsistente.

**Fix:** Aplicar el dimming a toda la tarjeta o al menos incluir el header:
```css
.match-locked { opacity: 0.65; }
```

---

### BUG-06 — Link "Regístrate" en Login cuando el registro no es público
**Archivo:** `Components/Pages/Account/Login.razor`, línea 43

```razor
<p class="text-center mt-3 mb-0 small">
    ¿No tienes cuenta? <a href="/register">Regístrate</a>
</p>
```

Según el plan original, las cuentas se crean solo vía migraciones/seed. Mostrar un link de registro público es confuso y potencialmente un vector de creación de cuentas no autorizadas si la página de registro no está correctamente protegida.

**Fix:** Eliminar el párrafo o reemplazarlo con "Contacta al administrador para obtener acceso."

---

### BUG-07 — `CreatedAt.ToLocalTime()` usa zona horaria del servidor
**Archivo:** `Components/Pages/Pools/Index.razor`, línea 73

```razor
<p class="text-muted small mb-0">
    Creada el @pool.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy")
</p>
```

`ToLocalTime()` usa el huso horario del servidor, no el del cliente. Si el servidor está en UTC y el usuario en Mexico City, la fecha puede mostrar el día anterior.

**Fix:** Usar `pool.CreatedAt.ToString("dd/MM/yyyy")` directamente sobre el valor UTC (ya que es solo fecha, la diferencia de horas es menor problema), o convertir con la zona del cliente.

---

### BUG-08 — Home.razor es una página placeholder vacía
**Archivo:** `Components/Pages/Home.razor`

```razor
<h1>Quiniela 2026</h1>
<p>Bienvenido. Ve a <a href="/pools">Mis Salas</a> para ver o unirte a una sala.</p>
```

La página de inicio no tiene utilidad. Cualquier usuario que accede a "/" ve solo este mensaje minimalista sin información relevante — sin salas del usuario, sin próximos partidos, sin posición en ranking, sin call-to-action.

**Fix:** Rediseñar Home como dashboard con widgets (ver sección 3).

---

## 2. Plan de Mejoras de Look & Feel

### 2.1 Sistema de Diseño — Nueva Paleta y Tipografía

**Paleta propuesta (inspirada en FIFA / Copa del Mundo):**

```css
:root {
    /* Primarios */
    --q-navy:       #0D1B2A;   /* Fondo oscuro profundo */
    --q-blue:       #1A56DB;   /* Azul activo (botones, links) */
    --q-gold:       #F4A261;   /* Dorado (ganadores, acentos) */
    --q-green:      #2D9E6B;   /* Verde cancha (success, saved) */

    /* Neutros */
    --q-surface:    #111827;   /* Cards en modo oscuro */
    --q-border:     #1F2937;   /* Bordes sutiles */
    --q-muted:      #6B7280;   /* Texto secundario */
    --q-text:       #F9FAFB;   /* Texto principal */

    /* Gradientes */
    --q-gradient-hero: linear-gradient(135deg, #0D1B2A 0%, #1A3A5C 50%, #0F2D1A 100%);
    --q-gradient-card: linear-gradient(145deg, #111827 0%, #1F2937 100%);
    --q-gradient-gold:  linear-gradient(135deg, #F4A261 0%, #FFD700 100%);
}
```

**Tipografía:**
- Mantener Nunito (ya cargada).
- Agregar peso 800 para títulos de impacto: `Nunito:wght@300;400;500;600;700;800;900`.
- Tamaños de heading con `clamp()` para que escalen en móvil.

---

### 2.2 Layout Global — Dark Mode con Sidebar Mejorada

**Cambios en `MainLayout.razor.css`:**
- Fondo `main` → `var(--q-navy)` o `#f0f4ff` (modo claro) en vez del blanco Bootstrap default.
- `top-row` → fondo `rgba(13,27,42,0.95)` con blur de backdrop, texto blanco real.
- Eliminar el border-bottom plano; reemplazar por línea de gradiente sutil.

**Cambios en `NavMenu.razor.css`:**
- Sidebar más ancha: 260px.
- Branding section con logo/escudo + nombre "Quiniela 2026".
- Ítems con `border-radius: 10px`, padding más generoso.
- Estado activo: fondo glassmorphism `rgba(255,255,255,0.15)` + borde izquierdo 3px gold.
- Hover: escala leve `transform: translateX(4px)` con transition 0.2s.
- Avatar del usuario en la parte inferior de la sidebar.
- Sección admin separada visualmente con un divider.

---

### 2.3 Página de Login — Rediseño Completo

**Objetivo:** Primera impresión impactante que comunique emoción futbolera.

**Layout:**
- Full-screen split: lado izquierdo con el canvas Three.js (ver sección 3); lado derecho (o centrado en móvil) con el formulario.
- Tarjeta con **glassmorphism**: `background: rgba(255,255,255,0.08)`, `backdrop-filter: blur(20px)`, `border: 1px solid rgba(255,255,255,0.15)`.
- Logo o escudo de la quiniela en la parte superior.
- Campos flotantes (`form-floating` de Bootstrap) con foco animado (borde gradiente).
- Botón "Entrar" → fondo gradiente `var(--q-gradient-gold)`, texto oscuro, escala on-hover.
- Separador Google más elegante.
- Fondo del body: `var(--q-gradient-hero)`.

---

### 2.4 Home — Dashboard Real

Reemplazar el placeholder con 4 widgets en grid:

```
┌─────────────────┬─────────────────┐
│  Tu posición    │  Próximos prono │
│  🥇 #1 de 8     │  3 pendientes   │
├─────────────────┴─────────────────┤
│  Racha actual: 5 aciertos seguidos │
├─────────────────┬─────────────────┤
│  Partidos hoy   │  Mis puntos     │
│  ⚽ 4 partidos  │  42 pts         │
└─────────────────┴─────────────────┘
```

- Cada widget es una card con ícono grande, número principal, subtítulo.
- Cards con hover: `transform: translateY(-4px)` + sombra ampliada.
- Animación de entrada: `fade-in-up` staggered (cada card con delay de 100ms).
- Link rápido a salas y pronósticos desde los widgets.

---

### 2.5 MatchCard — Rediseño Visual

**Cambios de estilo:**
- Fondo de card: blanco con sutil patrón de cancha en la parte superior (CSS background-image).
- Header: gradiente de fondo oscuro sutil → texto más legible.
- Banderas más grandes: 64px × 64px con sombra.
- Nombre del equipo: fuente más grande (0.9rem) en negrita.
- Opción seleccionada: no solo borde → fondo con gradiente azul al 15% + sombra interior de color + ícono de check animado que aparece con `scale(0) → scale(1)`.
- Sección de resultado (cuando finalizado): tabla de marcador estilo scoreboard con fondo oscuro y números grandes.
- Botón "Guardar": gradiente azul → verde en estado `savedOk`; transición suave de color.
- Estado bloqueado: overlay semi-transparente sobre toda la card + ícono de candado.

**Micro-interacciones:**
- `SelectOutcome` → pequeña vibración táctil con `navigator.vibrate(10)` vía JS interop.
- Al guardar con éxito → mini animación de check que sale desde el botón.

---

### 2.6 Tabla de Posiciones — Rediseño "Leaderboard"

**Cambios:**
- Header de podio para top-3: ocupan tarjetas especiales más grandes con avatar resaltado.
  ```
  [🥈 2do]  [🥇 1ro — más grande]  [🥉 3ro]
  ─────────────────────────────────────────
  4to  Juan Pérez        ........  28 pts
  5to  ...
  ```
- Fondo del #1: gradiente dorado sutil animado (shimmer effect).
- Barra de puntos: más gruesa (6px), con animación `width: 0% → Xw%` al entrar en viewport.
- Entrada staggered: cada fila aparece con `animation-delay: i * 60ms` al cargar la página.
- Puntos del usuario actual resaltados en azul brillante.

---

### 2.7 Páginas de Salas (Pools)

**Cards de salas:**
- Color de acento por sala (generado desde el nombre/id con HSL).
- Mostrar número de miembros y próximos partidos en la card.
- Hover: elevación + sombra coloreada.
- Formulario "unirse" con animación slide-down al mostrarse.

---

### 2.8 Animaciones y Transiciones Globales

Archivo nuevo: `wwwroot/css/animations.css`

```css
/* Entrada estándar */
@keyframes fadeInUp {
    from { opacity: 0; transform: translateY(20px); }
    to   { opacity: 1; transform: translateY(0); }
}

@keyframes fadeInScale {
    from { opacity: 0; transform: scale(0.92); }
    to   { opacity: 1; transform: scale(1); }
}

/* Shimmer para loading skeletons */
@keyframes shimmer {
    0%   { background-position: -200% center; }
    100% { background-position:  200% center; }
}

/* Check de éxito */
@keyframes popIn {
    0%   { transform: scale(0); opacity: 0; }
    70%  { transform: scale(1.2); }
    100% { transform: scale(1); opacity: 1; }
}

/* Pulso de alerta (partidos que cierran pronto) */
@keyframes urgentPulse {
    0%, 100% { box-shadow: 0 0 0 0 rgba(255, 193, 7, 0.4); }
    50%       { box-shadow: 0 0 0 8px rgba(255, 193, 7, 0); }
}

.animate-fade-in-up    { animation: fadeInUp 0.4s ease both; }
.animate-fade-in-scale { animation: fadeInScale 0.3s ease both; }
.skeleton-loading {
    background: linear-gradient(90deg, #f0f0f0 25%, #e0e0e0 50%, #f0f0f0 75%);
    background-size: 200% 100%;
    animation: shimmer 1.5s infinite;
}
```

**Loading states:**
- Reemplazar `<p class="text-muted">Cargando...</p>` con skeleton screens (placeholder gris animado con shimmer) en todas las páginas.

---

## 3. Plan de Animaciones Three.js

### Estrategia de integración en Blazor Server

Three.js se integra como módulo ES6 importado en `quiniela.js`. Blazor expone funciones que Blazor puede invocar vía `IJSRuntime`:

```javascript
// wwwroot/js/threejs-effects.js  (nuevo archivo)
import * as THREE from 'https://cdn.jsdelivr.net/npm/three@0.167.0/build/three.module.min.js';

export function initLoginScene(canvasId) { /* ... */ }
export function destroyScene(canvasId)   { /* ... */ }
export function triggerConfetti()        { /* ... */ }
```

En Blazor:
```csharp
await JS.InvokeVoidAsync("window.QuinielaThree.initLoginScene", "canvas-login");
```

Para evitar memory leaks, cada componente que inicia una escena Three.js debe llamar al destroy en `IAsyncDisposable.DisposeAsync()`.

---

### Efecto 1 — Login: Balones 3D flotantes (fondo)

**Archivo:** `wwwroot/js/three-login.js`

**Escena:**
- Canvas full-screen detrás del formulario de login.
- 12–15 balones de fútbol 3D (icosaedro + textura procedural de pentágonos negros).
- Cada balón flota con velocidad y rotación únicas, rebota suavemente en los bordes del viewport.
- Iluminación: `AmbientLight` tenue + `PointLight` en el tope izquierdo simulando reflector de estadio.
- Profundidad: algunos balones más grandes (foreground) y más pequeños (background) para efecto parallax.
- Partículas de fondo: 200 puntos (`THREE.Points`) formando una nube estelar/confeti muy sutil.
- Paleta: fondo del canvas transparente (el CSS maneja el gradiente de fondo); balones con material `MeshStandardMaterial` blanco y negro.
- Rendimiento: `requestAnimationFrame` con throttle a 30fps en móvil si `window.matchMedia('(prefers-reduced-motion)')` no está activo.

**Código base:**

```javascript
function createSoccerBall(scene) {
    const geo = new THREE.IcosahedronGeometry(1, 1);
    const mat = new THREE.MeshStandardMaterial({
        color: 0xffffff,
        roughness: 0.6,
        metalness: 0.1,
    });
    const ball = new THREE.Mesh(geo, mat);
    // Generar manchas negras con geometría adicional (dodecahedron overlay)
    return ball;
}
```

---

### Efecto 2 — Home/Standings: Luces de estadio animadas

**Archivo:** `wwwroot/js/three-stadium.js`

**Escena:**
- Canvas de 100% ancho × 200px de alto, en el hero/header de la página.
- Simulación abstracta de luces de estadio: 4 `SpotLight` que oscilan lentamente apuntando al centro.
- Terreno de juego simplísimo: plano verde con las líneas blancas del campo (marcas de textura procesal).
- Cámara fija desde arriba en ángulo oblicuo.
- Muy sutil, no distrae del contenido debajo.
- Se destruye al abandonar la página.

---

### Efecto 3 — MatchCard: Confetti al guardar pronóstico

**Librería alternativa:** En vez de Three.js completo (que puede ser pesado para este efecto puntual), usar **`canvas-confetti`** (4KB gzipped):

```html
<script src="https://cdn.jsdelivr.net/npm/canvas-confetti@1.9.3/dist/confetti.browser.min.js"></script>
```

**Disparador en Blazor:**

```csharp
// En MatchCard.razor, método Save():
if (success) {
    savedOk = true;
    await JS.InvokeVoidAsync("confetti", new {
        particleCount = 40,
        spread = 60,
        origin = new { y = 0.8 },
        colors = new[] { "#1A56DB", "#2D9E6B", "#F4A261", "#FFD700" }
    });
}
```

**Alternativa con Three.js** (si se prefiere coherencia de librería): Partículas 2D planas (sprites de balones pequeños) que explotan desde el botón "Guardar" y caen con gravedad.

---

### Efecto 4 — Tabla de Posiciones: Trofeo 3D para el #1

**Archivo:** `wwwroot/js/three-trophy.js`

**Escena:**
- Mini-canvas de ~80×80px en el widget del jugador #1.
- Trofeo 3D simplificado (lathed geometry rotando lentamente, color dorado).
- `MeshStandardMaterial` con `metalness: 0.9, roughness: 0.1, color: 0xFFD700`.
- Rotación continua en Y con `requestAnimationFrame`.
- Solo se instancia si hay al menos 1 partido finalizado.

---

### Efecto 5 — Cargador global: Balón rodante

**Archivo:** Inline en `ReconnectModal.razor` o como componente `<LoadingBall />`.

**Escena:**
- Canvas pequeño (60×60px) con un balón 3D rodando sobre su eje.
- Reemplaza el `spinner-border` de Bootstrap en todas las páginas mientras cargan.
- Reutilizable como componente Razor: `<ThreeBallLoader />`.

---

## 4. Priorización y Orden de Implementación

| Estado | # | Tarea | Impacto | Esfuerzo | Prioridad |
|---|---|---|---|---|---|
| ✅ | 1 | **BUG-01:** Agregar iconos faltantes en NavMenu | Alto | 30 min | 🔴 Inmediato |
| ✅ | 2 | **BUG-02:** Fix contraste texto blanco en top-bar | Alto | 15 min | 🔴 Inmediato |
| ✅ | 3 | **BUG-03:** Fix `lang="en"` → `lang="es"` | Bajo | 2 min | 🔴 Inmediato |
| ✅ | 4 | **BUG-06:** Eliminar link "Regístrate" del Login | Medio | 5 min | 🔴 Inmediato |
| ✅ | 5 | **BUG-05:** Fix dimming simétrico en tarjetas bloqueadas | Bajo | 10 min | 🟡 Rápido |
| ✅ | 6 | **BUG-04:** Animación de acordeón (cargar Bootstrap JS) | Medio | 1h | 🟡 Rápido |
| ✅ | 7 | **BUG-07:** Fix `CreatedAt.ToLocalTime()` en Pools | Bajo | 20 min | 🟡 Rápido |
| ✅ | 8 | Nuevo sistema de colores CSS variables | Alto | 2h | 🟢 Mejora |
| ✅ | 9 | Archivo `animations.css` global | Alto | 2h | 🟢 Mejora |
| ✅ | 10 | Rediseño Login page (glassmorphism + gradiente) | Muy alto | 3-4h | 🟢 Mejora |
| ✅ | 11 | Fix iconos NavMenu + hover animations | Alto | 2h | 🟢 Mejora |
| ✅ | 12 | Home.razor → Dashboard con widgets | Alto | 4-5h | 🟢 Mejora |
| ✅ | 13 | MatchCard rediseño visual + micro-interacciones | Alto | 4-6h | 🟢 Mejora |
| ✅ | 14 | Standings → Podio + staggered animations | Alto | 3-4h | 🟢 Mejora |
| ✅ | 15 | Skeleton loaders (reemplazar "Cargando...") | Medio | 2h | 🟢 Mejora |
| ✅ | 16 | **Three.js Efecto 1:** Balones flotantes en Login | Muy alto | 5-7h | 🔵 Three.js |
| ✅ | 17 | **Three.js Efecto 3:** Confetti al guardar pronóstico | Alto | 1-2h | 🔵 Three.js |
| ⬜ | 18 | **Three.js Efecto 4:** Trofeo 3D en #1 Standings | Medio | 3-4h | 🔵 Three.js |
| ✅ | 19 | **Three.js Efecto 2:** Luces de estadio en header | Medio | 4-5h | 🔵 Three.js |
| ✅ | 20 | **Three.js Efecto 5:** Balón 3D como loader | Bajo | 2-3h | 🔵 Three.js |

**Estimación total:** 50–70 horas de trabajo.

---

## 5. Archivos a Crear / Modificar

### Archivos a modificar:
| Archivo | Tipo de cambio |
|---|---|
| `Components/App.razor` | Fix `lang="es"`, agregar `bootstrap.bundle.min.js`, agregar CDN Three.js/confetti |
| `Components/Layout/MainLayout.razor` | Fix top-bar contraste |
| `Components/Layout/MainLayout.razor.css` | Nueva paleta, dark top-row |
| `Components/Layout/NavMenu.razor.css` | Agregar 3 iconos faltantes, animaciones hover |
| `Components/Pages/Account/Login.razor` | Rediseño completo, remover link registro |
| `Components/Pages/Home.razor` | Dashboard con widgets |
| `Components/Shared/MatchCard.razor` | Nuevo diseño, confetti JS interop |
| `Components/Pages/Standings/Index.razor` | Podio, staggered animations |
| `Components/Pages/Pools/Index.razor` | Fix `ToLocalTime()`, card redesign |
| `wwwroot/app.css` | Fix dimming, nuevos estilos base |

### Archivos a crear:
| Archivo | Propósito |
|---|---|
| `wwwroot/css/animations.css` | Keyframes y clases de animación global |
| `wwwroot/css/theme.css` | Variables CSS de paleta + componentes rediseñados |
| `wwwroot/js/three-login.js` | Escena Three.js balones flotantes |
| `wwwroot/js/three-trophy.js` | Trofeo 3D para standings |
| `Components/Shared/SkeletonLoader.razor` | Componente de loading skeleton reutilizable |
| `Components/Shared/ThreeBallLoader.razor` | Loader balón 3D (opcional) |

---

## 6. Notas de Rendimiento

- **Three.js en móvil:** Detectar `prefers-reduced-motion` y desactivar las escenas 3D si está activo. También desactivar si el dispositivo tiene menos de 4 núcleos (`navigator.hardwareConcurrency < 4`).
- **Lazy loading de Three.js:** Importar el módulo solo en las páginas que lo usan (módulos ES6 dinámicos: `import()`), no en el bundle global.
- **Canvas cleanup:** Siempre destruir la escena Three.js en `IAsyncDisposable` del componente Blazor para evitar memory leaks en navegación SPA.
- **`requestAnimationFrame` throttle:** En móvil limitar a 30fps; en desktop 60fps.
- **Confetti alternativo:** `canvas-confetti` pesa 4KB y es más adecuado que Three.js para un efecto de partículas 2D puntual. Usar Three.js solo donde el 3D real aporte valor visual.
