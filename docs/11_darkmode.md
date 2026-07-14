# 11 — Dark mode en toda la UI

**Fecha:** 2026-07-13
**Estado:** ✅ Completado (2026-07-13)
**Contexto:** El sitio hoy es tema claro fijo. Este documento define el plan para agregar **modo oscuro** a todos los módulos (Home, Salas, Pronósticos, Fases, Bracket, Campeón, Tabla/Evolución/Versus, Stats, Logros, Resumen diario, Log, Perfil, Admin), con toggle visible en todas las páginas y persistencia por dispositivo.

La buena noticia del análisis: **ya existe un sistema de design tokens** (`wwwroot/css/theme.css`, variables `--q-*`) y las páginas más recientes (DailySummary, Log, Versus, Achievements, History) ya lo consumen. El grueso del trabajo no es "diseñar un tema oscuro desde cero" sino (a) definir los valores oscuros de los tokens, (b) migrar el CSS viejo que hardcodea colores hacia los tokens, y (c) la infraestructura de switching sin flash.

---

## Decisiones de producto

| Tema | Decisión |
|------|----------|
| Mecanismo | **CSS custom properties**: un solo bloque `:root[data-theme="dark"]` en `theme.css` redefine los tokens `--q-*`. Los componentes NO llevan overrides oscuros propios — solo consumen tokens. Bootstrap se voltea con su soporte nativo `data-bs-theme="dark"` (v5.3.3 ya incluida). |
| Default | **Preferencia del sistema** (`prefers-color-scheme`) mientras el usuario no haya elegido. Al tocar el toggle se fija la elección manual y deja de seguir al sistema. |
| Persistencia | **`localStorage` por dispositivo** (clave `quiniela_theme`, valores `light`/`dark`; ausencia = sistema). Sin tabla ni migración — mismo criterio que `quiniela_push_dismissed` y las insignias vistas. |
| Ubicación del toggle | Botón ☀️/🌙 en el **top-row del MainLayout** (visible en todas las páginas autenticadas). Componente nuevo `ThemeToggle.razor` con `@rendermode="InteractiveServer"` **explícito en el punto de uso** — el MainLayout es SSR estático y sin eso el componente desaparece silenciosamente (precedente documentado: `NotificationConsent`, doc 07/N0). |
| Elementos ya oscuros | El sidebar, el top-row, el hero de Home, la página de Login (glassmorphism navy full-screen) y los bottom sheets navy (`MatchPredictionsSheet`, `ph-sheet`, `cmp-sheet`, `ach-sheet`, `ds-sheet`) **se quedan igual en ambos temas** — ya son oscuros por diseño y son la identidad visual del sitio. Dark mode afecta principalmente fondo, superficies, bordes y texto del área de contenido. |
| Login | **Exenta.** Ya es oscura full-screen con su propio layout (`FullScreenLayout`); no se toca (solo verificar que no herede nada raro de `data-bs-theme`). |
| Banderas e imágenes | Sin filtros. `flag-icons` y las fotos de perfil se ven bien sobre fondo oscuro. La pelota de fondo (`bg-ball`) puede bajar opacidad en oscuro si en QA se ve muy brillante — decisión visual, no bloqueante. |
| Alcance | Solo UI. Sin cambios de esquema de BD, servicios ni notificaciones. |

---

## Auditoría de CSS (2026-07-13)

Conteo de colores hardcodeados (`#hex`) vs. uso de tokens (`var(--q-*)`) por archivo:

| Archivo | hex | tokens | Situación |
|---------|----:|-------:|-----------|
| `wwwroot/app.css` | 41 | 0 | Home dashboard + podium de Standings — **migrar** |
| `Shared/MatchCard.razor.css` | 38 | 0 | Componente más visible del sitio — **migrar** |
| `Pages/Groups/Index.razor.css` | 33 | 11 | Mixto — **migrar** |
| `Shared/TeamSheet.razor.css` | 17 | 0 | Sheet navy — mayormente invariante, **revisar** |
| `Pages/Account/Login.razor.css` | 16 | 0 | Ya oscura — **exenta** |
| `Shared/KnockoutStageView.razor.css` | 12 | 5 | **Migrar** |
| `Shared/MatchPredictionsSheet.razor.css` | 11 | 0 | Sheet navy — invariante, **revisar** |
| `Pages/Standings/Versus.razor.css` | 10 | 54 | Casi migrado — **rematar** |
| `Pages/Bracket/Index.razor.css` | 8 | 0 | **Migrar** |
| `Pages/Champion/Index.razor.css` | 8 | 0 | **Migrar** |
| `Shared/BracketMatchCard.razor.css` | 8 | 0 | **Migrar** |
| `Pages/Stats/Index.razor.css` | 6 | 1 | **Migrar** |
| `Layout/MainLayout.razor.css` | 4 | 0 | Sidebar/top-row invariantes — casi nada |
| `Layout/ReconnectModal.razor.css` | 4 | 0 | **Revisar** |
| `Pages/Standings/History.razor.css` | 2 | 15 | **Rematar** |
| `Pages/Standings/Index.razor.css` | 1 | 5 | Podium vive en app.css — ver DM3 |
| `Pools/DailySummary` / `Pools/Log` / `Achievements` | 0–1 | 18–38 | **Ya tokenizados** — solo QA |

Hallazgos que simplifican:

- Los hex hardcodeados mapean casi 1:1 a tokens existentes: `#E2E8F0`→`--q-border`, `#64748B`→`--q-muted`, `#0F172A`→`--q-text`, `#475569`→`--q-text-sm`, `#1A56DB`→`--q-blue`, `#fff`→`--q-surface`, `#EEF2F7`→`--q-bg`. Solo falta un token para el gris-superficie sutil (`#F8FAFC`/`#F1F5F9`, muy usado como fondo de chips/filas alternas) — se agrega `--q-surface-2`.
- Los `style=""` inline en los `.razor` son **solo tamaños** (width/height/font-size), cero colores — no hay nada que migrar en el markup, con una excepción: clases utilitarias de Bootstrap.
- Clases Bootstrap que **no** voltean con `data-bs-theme` y hay que sustituir: `bg-white`, `text-dark`, `bg-light`. Concentradas en `Admin/Index.razor` (29 usos), `Standings/Index.razor` (13), `Stats/Index.razor` (11) y sueltas en ~10 archivos más. `text-muted`, `bg-body-*`, `text-body-*`, `table`, `card`, `form-control`, `badge` sí voltean solas.
- `#blazor-error-ui` ya declara `color-scheme: light only` — se queda así.

---

## Estrategia técnica

### Tokens oscuros (propuesta de paleta)

```css
/* theme.css — agregar después del :root existente */
:root[data-theme="dark"] {
    color-scheme: dark;               /* form controls y scrollbars nativos */

    --q-bg:      #0B1220;
    --q-surface: #131F31;
    --q-surface-2: #1B2A40;           /* token NUEVO (light: #F8FAFC) */
    --q-border:  #263650;
    --q-muted:   #8CA3C0;
    --q-text:    #E7EEF8;
    --q-text-sm: #B6C5DB;

    /* Acentos: se conservan (ya funcionan sobre navy en sidebar/sheets),
       solo se suavizan las sombras que en oscuro se ven sucias */
    --q-sh-sm:   0 1px 3px rgba(0,0,0,.35);
    --q-sh-md:   0 4px 10px rgba(0,0,0,.4);
    --q-sh-lg:   0 12px 28px rgba(0,0,0,.5);

    --q-g-header: linear-gradient(135deg, #0A1524 0%, #14233A 100%);
}
```

Los gradientes hero/sidebar/gold/blue/green y `--q-navy` no cambian (ya son oscuros o son acento). La paleta exacta se afina en QA visual; lo importante es que **todo pase por tokens** para que el ajuste sea de un solo archivo.

### Aplicación del tema sin flash (no-FOUC)

Script inline **al inicio del `<head>` de `App.razor`** (antes de los stylesheets), síncrono a propósito:

```html
<script>
    (function () {
        let t;
        try { t = localStorage.getItem('quiniela_theme'); } catch { }
        if (t !== 'light' && t !== 'dark')
            t = matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
        const d = document.documentElement;
        d.setAttribute('data-theme', t);
        d.setAttribute('data-bs-theme', t);
    })();
</script>
```

Blazor Server con SSR + circuito interactivo: el servidor no conoce `localStorage`, así que el atributo **siempre** lo pone el cliente — este script en el head corre antes del primer paint y también sobrevive a los enhanced navigations (el atributo vive en `<html>`, fuera de lo que Blazor re-renderiza).

### JS de soporte (`quiniela.js`)

```js
window.quiniela.theme = {
    get: () => document.documentElement.getAttribute('data-theme'),
    set: (t) => {
        const d = document.documentElement;
        d.setAttribute('data-theme', t);
        d.setAttribute('data-bs-theme', t);
        try { localStorage.setItem('quiniela_theme', t); } catch { }
        const m = document.querySelector('meta[name="theme-color"]');
        if (m) m.content = t === 'dark' ? '#0B1220' : '#0D1B2A';
    }
};
```

### Scoped CSS y el selector de tema

En los `.razor.css` NO se escriben bloques `[data-theme="dark"] ...` (el aislamiento de Blazor los compila bien — anexa el atributo de scope al selector final — pero abriría la puerta a duplicar reglas por tema). La regla del proyecto pasa a ser: **un `.razor.css` solo usa `var(--q-*)`, nunca un color literal de superficie/texto/borde**. Excepciones permitidas: colores dentro de los sheets navy y overlays `rgba(0,0,0,…)`/`rgba(255,255,255,…)` que son invariantes por diseño.

---

## Resumen de módulos

| # | Hecho | Módulo | Esfuerzo |
|---|:-----:|--------|----------|
| DM1 | [x] | Infraestructura: tokens oscuros, script no-FOUC, helpers JS, meta theme-color | ~2 h |
| DM2 | [x] | `ThemeToggle.razor` en el top-row + persistencia | ~2 h |
| DM3 | [x] | Migración CSS global: `app.css` (Home + podium), `animations.css` | ~2 h |
| DM4 | [x] | Migración de componentes compartidos (MatchCard, TeamSheet, sheets, bracket, skeleton) | ~3 h |
| DM5 | [x] | Migración de páginas con CSS hardcodeado (Groups, Champion, Bracket, Stats, Standings×3) | ~3 h |
| DM6 | [x] | Sustitución de utilitarias Bootstrap (`bg-white`/`text-dark`/`bg-light`) en Admin, Pools, Predictions, Profile, Standings, Stats | ~2 h |
| DM7 | [x] | QA transversal en ambos temas (checklist de rutas) + PWA (`offline.html`, manifest) | ~2 h |

**Orden sugerido:** DM1 → DM2 → DM3 → DM4 → DM5 → DM6 → DM7. DM1+DM2 dejan el switch funcionando end-to-end (con páginas a medio migrar, aceptable en dev); DM3–DM6 son independientes entre sí y se pueden hacer en cualquier orden o repartir; DM7 cierra.

---

## Módulo DM1 — Infraestructura del tema

**Alcance:** `theme.css`, `App.razor`, `quiniela.js`.

1. Agregar bloque `:root[data-theme="dark"]` a `theme.css` con la paleta propuesta (incluye `color-scheme: dark`).
2. Agregar token `--q-surface-2` también al `:root` claro (`#F8FAFC`) — lo consumen DM3–DM5.
3. Script inline no-FOUC al inicio del `<head>` de `App.razor` (ver Estrategia técnica).
4. `window.quiniela.theme.get/set` en `quiniela.js`.
5. `meta theme-color`: el valor actual `#0d6efd` (azul Bootstrap default) está off-brand — cambiar el estático a `#0D1B2A` y actualizarlo dinámicamente en `theme.set`.

**Criterios de aceptación**
- [x] Con `localStorage.quiniela_theme = 'dark'` puesto a mano, el sitio carga oscuro **sin flash claro** (script síncrono al inicio del `<head>`, antes de los stylesheets; verificado con Playwright).
- [x] Sin clave en localStorage, el tema sigue a `prefers-color-scheme` del SO (verificado con `colorScheme: 'dark'` en Playwright).
- [x] `data-theme` y `data-bs-theme` quedan sincronizados en `<html>` tras carga y tras enhanced navigation (sweep de 19 rutas × 2 temas × 2 viewports sin desincronización).
- [x] Inputs, selects y scrollbars se ven oscuros en dark (efecto de `color-scheme`; verificado en /admin y /pools/create).

## Módulo DM2 — ThemeToggle

**Alcance:** `Components/Shared/ThemeToggle.razor(.css)`, `MainLayout.razor`.

1. Componente botón ☀️/🌙: en `OnAfterRenderAsync(firstRender)` lee el tema actual vía interop (`quiniela.theme.get`), al clic llama `quiniela.theme.set` con el opuesto y actualiza su icono.
2. Colocarlo en el top-row de `MainLayout.razor`, antes del nombre de usuario, **con `@rendermode="InteractiveServer"` en el punto de uso** (gotcha documentado del layout estático — sin esto el componente no renderiza nada, sin error).
3. Fuera del `AuthorizeView` (el tema aplica también deslogueado, p. ej. en `/not-found`).
4. El toggle NO re-renderiza nada de Blazor al cambiar tema — el cambio es 100 % CSS vía atributo en `<html>`, sin `StateHasChanged` en cascada ni round-trip por página.

**Criterios de aceptación**
- [x] El toggle aparece en todas las páginas con MainLayout, en mobile (390 px) y desktop (1280 px).
- [x] Cambiar tema es instantáneo, persiste tras recargar y tras cerrar/abrir el navegador (clave `quiniela_theme` verificada en contexto nuevo).
- [x] El icono inicial coincide con el tema efectivo (incluido el caso "sistema" sin clave guardada); se sincroniza vía interop en `OnAfterRenderAsync`.

## Módulo DM3 — CSS global

**Alcance:** `wwwroot/app.css`, `wwwroot/css/animations.css`.

1. `app.css`: migrar a tokens las secciones Home dashboard (`.home-cta`, `.home-quick-actions`, `.home-qa-btn`, `.home-stat-matches-empty`, `.home-qa-title`) y podium (`.podium-card`). Los gradientes `home-welcome` / `home-stat-pools` / `home-stat-matches` son invariantes (ya oscuros/acento).
2. Podium 1/2/3: los tintes pastel (`#fffbeb`, `#f8fafc`, `#fff7ed`) no tienen token — definir 3 tokens dedicados (`--q-podium-1/2/3` + borde) con variante oscura (mismos matices oro/plata/bronce sobre base oscura, p. ej. `#3A2E12`-ish; afinar en QA).
3. Los estilos legacy de plantilla (`a` color `#006bb7`, `.btn-primary` `#1b6ec2`, focus ring blanco) están pisados por `theme.css` o son de plantilla Blazor — limpiar los muertos y tokenizar los vivos (el focus ring `0 0 0 0.1rem white` se ve mal en oscuro: usar `var(--q-bg)`).
4. `animations.css`: 3 hex — revisar y tokenizar si son de superficie.

**Criterios de aceptación**
- [x] Home completa legible en oscuro (tarjetas, quick actions, stat cards vacías).
- [x] Podium de Standings conserva la identidad oro/plata/bronce en ambos temas (tokens `--q-podium-1/2/3-bg/border` + `--q-podium-1-text`).
- [x] Ningún `#hex` de superficie/texto/borde queda en `app.css` — solo gradientes navy/acento invariantes (comentados) y el error boundary de plantilla.

## Módulo DM4 — Componentes compartidos

**Alcance:** `MatchCard`, `BracketMatchCard`, `KnockoutStageView`, `TeamSheet`, `MatchPredictionsSheet`, `NotificationConsent`, `SkeletonLoader`, `ReconnectModal`, `ThreeBallLoader`.

1. **MatchCard** (38 hex, el componente más visible): fondo/borde/texto/estados a tokens. Los estados acierto/fallo (`#F0FDF4` verde pastel, `#FCA5A5` rojo) necesitan variante oscura — tokens `--q-ok-bg`/`--q-bad-bg` o equivalente.
2. **BracketMatchCard + KnockoutStageView**: superficies y textos a tokens; el grayscale de eliminados no cambia.
3. **Sheets navy** (`TeamSheet`, `MatchPredictionsSheet`): son invariantes por decisión de producto — solo verificar que el **overlay** (`rgba(0,0,0,…)` + blur) siga viéndose bien sobre fondo oscuro y que ningún texto interno dependa de `--q-text` (que ahora cambia).
4. **SkeletonLoader**: el shimmer claro sobre fondo oscuro deslumbra — base `var(--q-surface-2)` y highlight sutil.
5. **ReconnectModal / NotificationConsent**: revisar los 4–2 hex; el banner de consentimiento ya es navy (invariante).

**Criterios de aceptación**
- [x] MatchCard en `/pools/{id}/predictions` y `/pools/{id}/my-predictions` correcta en ambos temas: pendiente, guardada, finalizada con acierto y con fallo, y variante KO (chips de instancia, badges de desglose). Badges usan `bg-success`/`bg-secondary` de Bootstrap que voltean solos.
- [x] Bracket (`/bracket`) y tab Dieciseisavos de `/fases` legibles en oscuro (verificado con screenshots).
- [x] Abrir TeamSheet y MatchPredictionsSheet en oscuro: el sheet se ve idéntico al claro y el overlay sigue separando (verificado abriendo el sheet en dark).
- [x] Skeletons no deslumbran en oscuro (shimmer sobre `--q-border`/`--q-surface-2`).

## Módulo DM5 — Páginas con CSS hardcodeado

**Alcance:** `Groups/Index` (33 hex), `Champion/Index` (8), `Bracket/Index` (8), `Stats/Index` (6), `Standings/Index` (+su parte en app.css), `Standings/History` (2), `Standings/Versus` (10).

1. Sustitución mecánica hex→token con el mapa de la auditoría; lo que no mapee (tintes de acierto, filas destacadas) usa los tokens nuevos de DM4.
2. `Standings/Index.razor.css`: los fondos oro/plata/bronce de filas usan los mismos tokens podium de DM3.
3. `History` y `Versus` ya están casi tokenizados — rematar los 2–10 hex restantes (ejes SVG del gráfico de evolución incluidos: el stroke del eje no puede ser gris claro fijo).
4. Páginas ya tokenizadas (`DailySummary`, `Log`, `Achievements`) — pasada de QA, sin trabajo esperado.

**Criterios de aceptación**
- [x] `/fases` (tabs Grupos y Dieciseisavos), `/pools/{id}/champion` (grilla de banderas + banner de estado), `/bracket`, `/pools/{id}/my-stats`, `/pools/{id}/standings` (+history +vs) legibles en ambos temas.
- [x] El gráfico SVG de evolución (líneas, puntos, ejes, labels) tiene contraste en oscuro (la línea usa `var(--q-blue)`; verificado en screenshot).
- [x] Grep de `#[0-9A-Fa-f]` en los `.razor.css` de estas páginas solo devuelve fallbacks de `var()` y el gradiente azul de acento de Stats (comentado como invariante).

## Módulo DM6 — Utilitarias Bootstrap

**Alcance:** ~15 archivos `.razor` con `bg-white` / `text-dark` / `bg-light` (Admin 29 usos, Standings 13, Stats 11, resto sueltos).

1. Sustituir: `bg-white` → `bg-body` o clase propia con `var(--q-surface)`; `bg-light` → `bg-body-tertiary` o `var(--q-surface-2)`; `text-dark` → `text-body`.
2. `Admin/Index.razor` es el mayor consumidor (tabs Por capturar / Finalizados / Bracket) y no tiene `.razor.css` propio — decidir en implementación si se crea uno con clases `adm-*` tokenizadas o si bastan las utilitarias `-body-*` de Bootstrap. Criterio: mínimo cambio que funcione en ambos temas.
3. Verificar componentes Bootstrap que voltean solos con `data-bs-theme` (cards, tables, forms, badges, accordion de Groups, breadcrumbs) — no tocarlos salvo que en QA se vean mal.
4. Páginas sin CSS propio (`Pools/Index`, `Create`, `Detail`, `Predictions/*`, `Profile`, `Home`, `NotFound`, `Error`): dependen de Bootstrap + app.css — pasada de revisión, se espera trabajo menor.

**Criterios de aceptación**
- [x] `/admin` completo (3 tabs, captura de marcador, corrección, chips de instancia, dropdowns de bracket) usable en oscuro.
- [x] Formularios (`/pools/create`, `/profile`, unirse por código en `/pools`) con inputs, validación (`.invalid`, `.validation-message`) y placeholders legibles en oscuro (validación tokenizada a `--q-green`/`--q-red`).
- [x] Grep de `bg-white|text-dark|bg-light` en `Components/` devuelve cero (`bg-warning text-dark` se sustituyó por `text-bg-warning`).

## Módulo DM7 — QA transversal y PWA

1. **Checklist de rutas** — cada una en claro y oscuro, mobile 390 px y desktop 1280 px:
   - [x] `/` (Home) · [x] `/login` (exenta: verificar que sigue igual) · [x] `/profile`
   - [x] `/pools` · [x] `/pools/create` · [x] `/pools/{id}` (Detail)
   - [x] `/pools/{id}/predictions` · [x] `/pools/{id}/my-predictions`
   - [x] `/pools/{id}/standings` · [x] `…/standings/history` · [x] `…/standings/vs`
   - [x] `/pools/{id}/champion` · [x] `/pools/{id}/achievements` (+ bottom sheet + medallas)
   - [x] `/pools/{id}/my-stats` · [x] `/pools/{id}/daily-summary` · [x] `/pools/{id}/log`
   - [x] `/fases` (ambos tabs) · [x] `/bracket` · [x] `/admin` (3 tabs)
   - [x] `/not-found` · [x] banner de NotificationConsent (CSS navy invariante, sin cambios) · [x] ReconnectModal (verificado con `context.setOffline(true)` en dark)
2. Verificación con Playwright ad-hoc (patrón de sesiones previas: npm install en scratchpad, login real contra BD dev, screenshots por ruta × tema × viewport). Revisar además 0 errores de consola al alternar el toggle en cada página.
3. **PWA:** `offline.html` (hardcodea estilos propios — darle soporte mínimo con `prefers-color-scheme`, no depende de localStorage del app shell), `manifest.webmanifest` (`background_color`/`theme_color` — el splash de PWA no puede ser dinámico; decidir un valor único, propuesta: navy `#0D1B2A`, neutral en ambos temas).
4. Contraste: spot-check WCAG AA (4.5:1 texto normal) de `--q-muted` y `--q-text-sm` sobre `--q-surface`/`--q-bg` oscuros con un checker. ✅ Verificado: `--q-muted` #8CA3C0 sobre `--q-surface` #131F31 ≈ 6.4:1; `--q-text-sm` #B6C5DB queda por encima; ambos pasan AA.

### Notas de implementación (2026-07-13)

- Tokens nuevos además de `--q-surface-2`: `--q-ok-bg/-border`, `--q-bad-bg/-border` (estados de predicción), `--q-blue-tint` (selecciones/acordeones activos), `--q-va-*` (botón "ver pronósticos de todos") y `--q-podium-1/2/3-bg/-border` + `--q-podium-1-text` — todos con variante clara y oscura en `theme.css`.
- QA ejecutado con Playwright contra BD dev (usuario `admin`): 19 rutas × 2 temas × 2 viewports, 0 errores de consola, atributos `data-theme`/`data-bs-theme` siempre sincronizados; sheets navy y overlay verificados en oscuro; ReconnectModal verificado con `setOffline`.
- El "flash claro" queda estructuralmente imposible: el script del tema es el primer nodo del `<head>`, antes de cualquier stylesheet.
- **Fix post-QA (mismo día):** el tema se perdía al navegar entre páginas — Blazor borra los atributos de `<html>` al sincronizar con el documento del servidor (ver gotcha 4 actualizado). Resuelto con `MutationObserver` + `quiniela.theme.apply()` en `quiniela.js`; re-verificado con Playwright navegando 7 rutas con dark activo.

---

## Gotchas conocidos del proyecto (leer antes de implementar)

1. **MainLayout es SSR estático**: todo componente interactivo que se agregue ahí necesita `@rendermode="InteractiveServer"` en el punto de uso o desaparece sin error (doc 07/N0). Aplica a `ThemeToggle`.
2. **Scoped CSS**: el aislamiento reescribe selectores; la estrategia es no escribir overrides `[data-theme]` en `.razor.css` — solo tokens.
3. **`#blazor-error-ui`** ya tiene `color-scheme: light only` — dejarlo.
4. **Enhanced navigation**: no confiar en que un componente re-aplique el tema al navegar; el atributo vive en `<html>` y el script del head cubre cold loads. El toggle solo muta el atributo. **⚠️ Corrección post-implementación (2026-07-13):** la premisa "el atributo vive fuera de lo que Blazor re-renderiza" resultó FALSA — al navegar entre páginas, Blazor sincroniza los atributos de `<html>` con el documento del servidor (que no trae `data-theme`) y los **borra**, sin disparar ningún evento `blazor:*` observable (`blazor:enhancedload`/`blazor:navigated` no dispararon en el diagnóstico). Fix: `MutationObserver` en `quiniela.js` sobre `data-theme`/`data-bs-theme` de `<html>` que re-aplica el tema desde localStorage/sistema cuando desaparecen. Verificado con Playwright: toggle a dark + navegación por 7 rutas vía links reales, el tema persiste en todas.
5. **Playwright + sticky/fullPage**: los screenshots `fullPage:true` muestran artefactos con `position:sticky` (barras de totales, headers) — usar viewport normal + scroll para verificar esos elementos (doc mejoras_1/J).
6. **No mutar datos de la BD dev** para QA visual: todo el QA de este plan es de solo lectura (navegar y alternar tema), no requiere finalizar partidos ni insertar filas.
