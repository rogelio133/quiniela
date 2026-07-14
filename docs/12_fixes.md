# 12 — Fixes de UI/UX

Plan de desarrollo para 7 ajustes reportados. Cada fix es independiente y se puede implementar/probar por separado.

---

## ✅ [FINALIZADO] Fix 1 — Campeón: texto y bandera al doble de tamaño (solo equipo seleccionado)

**Problema:** En el módulo de campeón (`/pools/{id}/champion`) el nombre del equipo y la bandera se ven muy pequeños. El aumento debe aplicar **únicamente a la celda del equipo ya seleccionado** (`.champ-cell-selected`); las demás celdas de la cuadrícula conservan su tamaño actual.

**Archivos:**
- `src/Quiniela.Web/Components/Pages/Champion/Index.razor.css`

**Plan:**
1. Agregar regla `.champ-cell-selected .champ-cell-flag` que duplique el tamaño actual de la bandera (width/height/font-size según cómo esté definida, las banderas son `fi fi-xx` de flag-icons). No tocar la regla base `.champ-cell-flag` (línea ~42).
2. Agregar regla `.champ-cell-selected .champ-cell-name` que duplique el `font-size`. No tocar la regla base `.champ-cell-name` (línea ~48).
3. Revisar que `.champ-grid` siga acomodando bien la celda agrandada junto a las normales (la fila de la celda seleccionada crecerá; verificar que no se corten nombres largos como "Estados Unidos" y que el cambio de tamaño al seleccionar/deseleccionar no cause saltos bruscos de layout — considerar una `transition`).
4. Verificar también el estado bloqueado (`.champ-flag-big`, `.champ-locked-team`) — el cambio aplica solo a la celda seleccionada de la cuadrícula, pero confirmar que visualmente sigan consistentes.

---

## ✅ [FINALIZADO] Fix 2 — Evolución: imposible seleccionar puntos en la línea de tiempo (móvil/producción)

**Problema:** En `/pools/{id}/standings/history` cada punto de la gráfica es un `div.ph-point` de **7×7 px** (`History.razor.css:114-122`). En producción ya hay muchos partidos, así que los puntos quedan pegados entre sí y el área táctil es minúscula — en móvil es prácticamente imposible atinarle (ver `evolucion.jpeg`). El mínimo recomendado de área táctil es ~44×44 px.

**Recomendación (elegida):** dejar de depender de tocar el punto exacto y hacer que **todo el área de la gráfica sea táctil, con snap al punto más cercano**:

- Un solo handler de tap/click sobre `.ph-chart`; con la coordenada X del toque se calcula el índice del punto más cercano (`round(x / width * (n-1))`) y se abre el sheet de detalle de ese punto.
- Esto escala sin importar cuántos partidos haya: el usuario toca "por ahí" y siempre selecciona algo.

*Alternativa descartada:* agrandar los puntos o su hit-area invisible — con 40+ partidos en un ancho de ~300px los hit-areas se traslapan igual; no resuelve el problema de fondo. *Alternativa opcional a futuro:* un scrubber (`input type="range"`) debajo de la gráfica para arrastrar entre partidos.

**Archivos:**
- `src/Quiniela.Web/Components/Pages/Standings/History.razor`
- `src/Quiniela.Web/Components/Pages/Standings/History.razor.css`
- `src/Quiniela.Web/wwwroot/js/*.js` (helper de interop, si se necesita el ancho real)

**Plan:**
1. En `History.razor`, quitar el `@onclick` individual de cada `.ph-point` (se conservan como marcadores visuales, con `pointer-events: none`).
2. Agregar `@onclick` sobre el contenedor `.ph-chart` que reciba `MouseEventArgs`. Usar `e.OffsetX` y el ancho del contenedor para calcular el índice: `i = Math.Clamp((int)Math.Round(offsetX / width * (series.Count - 1)), 0, series.Count - 1)`.
3. Para el ancho del contenedor: obtenerlo vía interop JS (`getBoundingClientRect().width` con `ElementReference`) al momento del click, o registrar un helper pequeño en JS que reciba el evento y regrese `{offsetX, width}` de una vez.
4. Llamar `OpenDetail(...)` con el punto calculado (misma lógica actual del sheet).
5. Feedback visual: resaltar el punto seleccionado (clase `.ph-point-active` con mayor tamaño/color) mientras el sheet está abierto.
6. Subir la altura de `.ph-chart` de 64px a ~96px para dar más área táctil vertical, y agregar `cursor: pointer` + `touch-action: manipulation` al contenedor.

---

## ✅ [FINALIZADO] Fix 3 — Admin/Log: filtro por módulo (página)

**Problema:** El log de visitas (`/pools/{id}/log`) solo permite filtrar por miembro; falta filtro por módulo/página.

**Archivos:**
- `src/Quiniela.Web/Services/PageVisitService.cs`
- `src/Quiniela.Web/Components/Pages/Pools/Log.razor`

**Plan:**
1. En `PageVisitService`:
   - Extender `GetPageAsync(int poolId, int? userId, int page, int pageSize)` con un parámetro `string? pageName` y aplicar `Where(v => v.PageName == pageName)` cuando venga.
   - Agregar `GetDistinctPageNamesAsync(int poolId)` que regrese los `PageName` distintos registrados en esa sala (para poblar el dropdown solo con módulos que sí tienen visitas).
2. En `Log.razor`:
   - Agregar un segundo `<select class="form-select form-select-sm log-filter">` con "Todos los módulos" + los nombres distintos, junto al filtro de miembros (envueltos en un contenedor flex para que en móvil queden apilados o lado a lado).
   - Nuevo estado `filterPageName`; al cambiar cualquiera de los dos filtros, resetear `page = 1` y recargar.
   - Ajustar el mensaje de "sin visitas" para contemplar la combinación de filtros.

---

## ✅ [FINALIZADO] Fix 4 — Resumen diario: nombre del equipo en lugar de la clave

**Problema:** En `/pools/{id}/summary` los partidos muestran la clave (`ShortCode`, ej. "MEX") en lugar del nombre ("México").

**Archivos:**
- `src/Quiniela.Web/Components/Pages/Pools/DailySummary.razor`
- `src/Quiniela.Web/Components/Pages/Pools/DailySummary.razor.css`

**Plan:**
1. Invertir la prioridad del helper `TeamCode` (línea ~344): crear/renombrar a `TeamLabel(Team?)` que regrese `team?.Name ?? team?.ShortCode ?? "?"`.
2. Aplicar en los tres lugares que hoy usan clave: equipos local/visitante de cada fila (líneas ~95 y ~100), el pronóstico de campeón (línea ~137, cambiar a `summary.Champion.TeamName ?? summary.Champion.ShortCode`) y `PredictionLabel` (líneas ~338-339).
3. En el CSS, ajustar `.ds-team-code` para nombres largos: reducir un poco el font-size si es necesario y agregar `overflow: hidden; text-overflow: ellipsis; white-space: nowrap;` con un `max-width` o `flex: 1 1 auto` para que "Estados Unidos vs Países Bajos" no rompa el layout de la fila en móvil.

---

## ✅ [FINALIZADO] Fix 5 — Header: nombre a la izquierda, dark mode al centro, salir a la derecha

**Problema:** En la barra superior (`top-row`) todo está alineado a la derecha: toggle de tema, nombre y botón Salir. Se quiere: **nombre a la izquierda, toggle al centro, menú/Salir a la derecha**.

**Archivos:**
- `src/Quiniela.Web/Components/Layout/MainLayout.razor` (líneas 11-25)

**Plan:**
1. Reestructurar el `top-row` en tres zonas con flexbox:
   ```
   <div class="top-row px-4 d-flex align-items-center justify-content-between">
       <span>nombre</span>                 ← izquierda
       <ThemeToggle />                     ← centro
       <form>Salir</form>                  ← derecha
   </div>
   ```
   Usando `justify-content-between` directo, o tres contenedores con `flex: 1` y el del centro con `text-align: center` si se quiere centrado geométrico exacto.
2. Mover el `<span>` del DisplayName dentro de `<Authorized>` al inicio, el `ThemeToggle` (que hoy está fuera del `AuthorizeView`) al centro, y el form de logout al final. Como el `ThemeToggle` está fuera del `AuthorizeView`, la estructura de tres zonas debe funcionar también para `<NotAuthorized>` (link "Iniciar sesión" a la derecha).
3. Verificar en móvil que el nombre largo no empuje al toggle (agregar `text-truncate` con `min-width: 0` al bloque del nombre).

---

## ✅ [FINALIZADO] Fix 6 — Pronosticar / Mis pronósticos: nombre del país grande, clave abajo

**Problema:** En las tarjetas de partido el texto grande es la clave (`mc-code`) y el nombre del país aparece pequeño abajo (`mc-name`). Se quiere invertir: nombre grande, clave pequeña abajo.

**Archivos:**
- `src/Quiniela.Web/Components/Shared/MatchCard.razor` (líneas ~47-48 y ~71-72)
- `src/Quiniela.Web/Components/Shared/MatchCard.razor.css`

Nota: `MatchCard` es compartido — el cambio aplica automáticamente a Pronosticar (`Predictions/Index`), Mis pronósticos (`MyPredictions`) y cualquier otra página que lo use (verificar Grupos/Bracket y decidir si ahí también se quiere).

**Plan:**
1. En el markup, invertir contenido: el elemento grande muestra `@Match.HomeTeam.Name.ToUpperInvariant()` y el pequeño `@TeamShort(...)` (ídem visitante).
2. La opción más limpia es intercambiar solo el contenido y conservar las clases CSS (`mc-code` sigue siendo "texto grande", `mc-name` "texto chico"), o bien renombrar clases a `mc-primary` / `mc-secondary` para que el CSS quede semánticamente correcto.
3. En el CSS, ajustar el font-size del texto grande para nombres largos: agregar `ellipsis`/`clamp` o reducir tamaño en pantallas chicas para "Estados Unidos", "Países Bajos", "Arabia Saudita".
4. Probar tarjetas en los 3 estados: pronosticable, bloqueada y finalizada.

---

## ✅ [FINALIZADO] Fix 7 — Tabla de posiciones: campeón eliminado en gris y con nombre

**Problema:** En `/pools/{id}/standings` cada jugador muestra su pick de campeón (`👑 bandera + ShortCode`). Si ese equipo ya fue eliminado, no hay indicación visual; además se quiere mostrar el **nombre** en lugar de la clave.

**Archivos:**
- `src/Quiniela.Web/Components/Pages/Standings/Index.razor` (líneas ~134-139 y ~190-195, top-3 y resto)
- `src/Quiniela.Web/Services/ChampionService.cs` (o donde viva la lógica de eliminación)

**Plan:**
1. **Dato de eliminación:** el módulo Campeón ya calcula `isEliminated` para el pick propio (`Champion/Index.razor`). Reutilizar esa lógica en `ChampionService`: exponer algo como `GetEliminatedTeamIdsAsync()` (equipos que ya no siguen vivos en el torneo) o cambiar `GetAllPredictionsForPoolAsync` para regresar `(Team, bool IsEliminated)`.
2. En `Standings/Index.razor`, cargar el set de eliminados en `LoadDataAsync` junto con `championPicks`.
3. En los dos bloques de render del pick (top-3 y resto):
   - Mostrar `@(champTeam.Name ?? champTeam.ShortCode)` en lugar de `ShortCode` primero.
   - Si está eliminado: agregar clase a la bandera con `filter: grayscale(1); opacity: .6;` y el nombre en gris/tachado (definir clase `champ-pick-eliminated` en el CSS de la página en lugar de estilos inline, que hoy abundan en ese bloque).
4. Ambos bloques renderizan lo mismo — vale la pena extraer el pick a un fragmento/`RenderFragment` local para no duplicar la lógica de eliminado dos veces.

---

## Orden sugerido de implementación

| # | Fix | Esfuerzo | Riesgo |
|---|-----|----------|--------|
| 1 | Fix 4 — Resumen diario (nombres) | Bajo | Bajo |
| 2 | Fix 1 — Campeón (tamaños) | Bajo | Bajo |
| 3 | Fix 5 — Header (layout) | Bajo | Bajo |
| 4 | Fix 6 — MatchCard (nombre/clave) | Medio | Medio (componente compartido) |
| 5 | Fix 3 — Log (filtro módulo) | Medio | Bajo |
| 6 | Fix 7 — Standings (campeón eliminado) | Medio | Medio (lógica de eliminación) |
| 7 | Fix 2 — Evolución (tap en gráfica) | Alto | Medio (interop JS) |

## Verificación

- Probar todo en viewport móvil (~390px) además de escritorio — la app se usa principalmente en celular.
- Verificar ambos temas (light/dark) en los cambios de CSS (Fixes 1, 2, 6, 7).
- Fix 2: probar en una sala con muchos partidos finalizados (el caso de producción del screenshot).
- Fix 6: revisar todas las páginas que consumen `MatchCard`, no solo Pronosticar/Mis pronósticos.
