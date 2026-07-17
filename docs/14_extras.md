# 14 — Extras del Resumen final (RF8) 🎁

**Fecha:** 2026-07-17
**Estado:** 🚧 En implementación — X1 y X2 completados (2026-07-17)
**Contexto:** Plan de desarrollo de RF8 del doc 13 (`docs/13_resumenfinal.md`). De los 6 extras propuestos ahí quedan **fuera de alcance** E3 (💬 Muro de despedida) y E6 (📄 PDF de recuerdo) por decisión del 2026-07-17 — con eso la pregunta abierta 2 del doc 13 queda cerrada. Alcance final: **E1 (Quiniela Awards), E2 (Rewind de la tabla), E4 (Bracket: pronóstico vs realidad) y E5 (Trivia del recuerdo)**, más el pase visual final de toda la página (criterio que quedó abierto en el doc 13).

Al no entrar E3, se mantiene la premisa del doc 13: **cero migraciones nuevas** — todo se calcula on-demand desde tablas existentes, y casi todo reusa datos que `FinalSummaryService` ya carga para la página.

---

## Resumen de módulos

| Módulo | Contenido | Depende de | Esfuerzo | Estado |
|--------|-----------|------------|----------|--------|
| X1 | 🏅 Quiniela Awards — stats de sala presentadas como ceremonia de premios | RF3 (hecho) | Bajo (presentación) | ✅ Completado (2026-07-17) |
| X2 | 🎥 Rewind de la tabla — "▶ Revivir el torneo" sobre la gráfica de evolución | RF5 (hecho) | Medio | ✅ Completado (2026-07-17) |
| X5 | Pase visual final de la página completa (criterio pendiente del doc 13) | X1–X4 | Bajo | ⏳ Pendiente |

Orden recomendado de implementación: **X1 → X2 → X5**. X1 primero porque redefine la presentación de "La sala en números" y conviene fijarla antes de sumar secciones nuevas; X5 al final porque es el QA de cierre de toda la página.

---

## X1 — 🏅 Los Quiniela Awards (E1)

Convertir las stats de sala en una **ceremonia de premios con nombres cómicos**: "premio + ganador + dato". Es la MISMA data de `FinalSummaryService.GetPoolStatsAsync` — cero queries nuevas; solo cambia el markup/copy de la sección "La sala en números" de `FinalSummary.razor`.

- [x] **Mapeo stat → premio.** Definir nombre de premio para las stats de sala que tienen "ganador" persona (las que son de partido/sala sin persona se quedan como tarjeta de stat normal). Propuesta inicial (ajustable en implementación): *(Implementado con la tabla tal cual, con 3 ajustes menores: el 🐺 se llama "El Lobo Estepario", los reyes de fase usan 🗂️/⚔️ —consistentes con las filas que ya usaba la tarjeta— en vez de 🕐 compartido, y el 🗿 "El Monje" viene de `ConstantStat`. "El Profeta y los Corazones Rotos" es una sola tarjeta ancha que conserva la lista completa de picks (`fs-faith-list`) para no perder a los que ni acertaron ni fueron eliminados.)*

  | Premio | Stat de origen (record ya existente) |
  |--------|--------------------------------------|
  | 🔮 El Nostradamus | `StreakStat` (mejor racha, #2) |
  | 🎰 El Veleta | `IndecisiveStat` (más indeciso, #7) |
  | ⏰ El Kamikaze | `AgonicChangeStat` (cambio más agónico, #6) |
  | 🐺 El Lobo Estepario | `LoneWolfStat` (acierto más solitario, #3) |
  | 🚀 El Remontador | `ComebackStat` (la remontada, #4) |
  | 🥶 El Gafado | `StreakStat` inversa (peor racha, #8) |
  | 🌞 El Consentido del Sol / 🥴 El Ancla | `DailyMedalsStat` (medallero diario, #21) |
  | 👻 El Fantasma | `GhostStat` (puntos dejados en la mesa, #20) |
  | 🦉 El Búho | `NightOwlStat` (#24) |
  | ⚡ El Madrugador / 🔥 El del Último Minuto | `AnticipationStat` (#25) |
  | 🗿 El Monje | `ConstantStat` (más constante, #29) |
  | 👑 El Profeta / 💔 Corazones rotos | `ChampionFaithRow` (fe en el campeón, #22) |
  | 🕐 Rey de Grupos / Rey del KO | `StageKingsStat` (#23) |

- [x] **Markup:** tarjeta de premio (`fs-award-card`) con: emoji grande + nombre del premio (tipografía display, protagonista), ganador con avatar (`ProfilePicturePath`, mismo patrón `MemberRef` de la página) y el dato como subtítulo ("cambió 14 veces sus pronósticos"). Reusa el reveal por scroll de RF3 (`fs-reveal` + stagger `--fs-i`) — cada premio "se entrega" al entrar al viewport, que ya da el efecto ceremonial sin JS nuevo. *(Ganador como píldora avatar+nombre (`AwardWinners`, RenderFragment); cinta dorada `--q-g-gold` arriba de cada tarjeta como toque ceremonial. Los premios dobles —Consentido/Ancla, Madrugador/Último Minuto, Reyes— van en una tarjeta ancha con `fs-award-halves` (grid auto-fit: con una sola mitad no queda descentrada).)*
- [x] **Reestructura de la sección:** "La sala en números" se divide en dos bloques: **"🏅 Los Quiniela Awards"** (premios con ganador) y **"La sala en números"** (stats de partido/colectivas sin persona: engaño colectivo, sorpresa, día dorado/negro, alergia al empate, equipo maldito/talismán, totales Wrapped, etc.). Mismo comportamiento actual de auto-ocultar tarjetas cuyas stats no sean computables. *(Awards primero, luego la colectiva. El caso `Ghost` sin fantasma —nadie dejó pasar partidos— se queda como tarjeta colectiva "Cero olvidos"; el premio 👻 solo existe si hay persona. `PenaltyProphets` (#19) y `DecidedItAll` (#13) no están en el mapeo del doc y siguen como stat colectiva. El helper `Names()` quedó sin uso (las rachas ahora muestran avatares) y se eliminó.)*
- [x] **Empates:** si un premio tiene ganadores múltiples (p. ej. racha compartida — `StreakStat.Holders` ya es lista), la tarjeta muestra a todos. *(`AwardWinners` recibe la lista completa y pinta una píldora por holder.)*
- [x] Dark/light con tokens `--q-*`; count-up (`data-countup`) donde el dato sea numérico, igual que RF3. *(9 count-ups en las tarjetas de premio; el número resaltado en dorado `--q-gold` como en las stats.)*

## X2 — 🎥 Rewind de la tabla (E2)

Botón **"▶ Revivir el torneo"** en la tarjeta de la gráfica de evolución (`fs-evo-card`): la gráfica se reproduce sola partido a partido — las líneas avanzan, los marcadores de cambio de líder van apareciendo, y al final remata el 🏆 del campeón.

- [x] **Reusar el mecanismo del wipe de RF5:** el dibujo inicial ya funciona con `clip-path` sobre el SVG completo disparado por `.fs-visible`, y cada marcador aparece cuando el frente pasa por su `--evo-x`. El rewind es el mismo wipe re-disparado con duración larga (~8–12 s vs la del reveal): quitar la clase que deja la gráfica "dibujada", forzar reflow y re-agregarla con una clase modificadora (`fs-evo-replay`) que alarga la `transition-duration`. Sin JS nuevo de animación — solo el toggle (puede ser 100% estado Blazor + CSS, patrón del resaltado de leyenda de RF5). *(Implementado 100% estado Blazor + CSS, pero el restart no fue con reflow: se cambia el `animation-name` (keyframes duplicados `fsEvoWipeReplay`/`fsEvoPtInReplay`/`fsEvoCupInReplay`) — reusar el mismo nombre no re-dispara una animación completada, cambiarlo sí. Estados: `fs-evo-replay` (wipe 10 s, marcadores a 100 ms/%, 🏆 a los 10.4 s) → `fs-evo-done` (deja la gráfica fija; sin ella, volver al estado base re-dispararía el wipe corto del reveal). Gotcha real encontrado en verificación: al cambiar la clase de estado, el diff de Blazor reescribe el atributo `class` y se lleva el `.fs-visible` que `observeReveals` agregó por JS (y no vuelve — el observer hace unobserve al primer disparo), dejando la tarjeta en `opacity: 0`; `fs-evo-replay`/`fs-evo-done` reponen el estado revelado por sí mismas.)*
- [x] **Indicador de progreso (opcional, decidir en implementación):** un rótulo que muestre el partido "actual" bajo la gráfica durante el replay. Nota técnica: con transiciones CSS puras no hay callback por punto — si se quiere el rótulo, el avance se maneja desde Blazor con un timer (`PeriodicTimer`, un tick por partido) actualizando `clip-path` inline y el rótulo juntos; si no se quiere, basta el CSS. Empezar por la versión CSS y evaluar si el rótulo aporta. *(Se quedó la versión CSS pura, sin rótulo ni timer por partido — la reproducción visual con los marcadores apareciendo en su momento ya cuenta la historia; cierra la pregunta abierta 2.)*
- [x] **Interacción:** el botón se deshabilita durante la reproducción (o cambia a "⏸/⏹"); el resaltado por leyenda de RF5 sigue funcionando durante y después del replay. Al terminar, la gráfica queda en su estado normal completo. *(Se tomó la variante ⏹: durante el replay el botón cambia a "⏹ Detener" y detener salta directo al estado final (cancela el `Task.Delay` de 11 s vía `CancellationTokenSource`, que también se cancela en `DisposeAsync`). El resaltado sigue vivo porque las animaciones de replay solo tocan `animation`, y al terminar (sin fill forwards en los marcadores) la opacidad vuelve al cascade donde `fs-evo-dim`/`fs-evo-hi` operan — mismo truco que ya usaba el reveal.)*
- [x] **`prefers-reduced-motion`:** el botón no se muestra (la gráfica ya aparece dibujada de inmediato, coherente con RF5). *(Doble cobertura: el markup lo omite vía el flag `reducedMotion` que la página ya leía por JS, y el media query oculta `fs-evo-replay-row` para el primer render pre-interop.)*
- [x] Se oculta junto con la gráfica cuando `GetEvolutionAsync` devuelve null (<2 partidos con historial). *(El botón vive dentro del mismo `@if (evolution is not null)` de la tarjeta.)*
- [x] **Extra pedido post-implementación (2026-07-17): la foto de cada jugador recorre su línea durante el replay.** Un `@keyframes` por jugador generado inline desde Blazor (`EvoRunnerKeyframes()`) con un stop por partido (`left`/`top` en %): como el eje X del wipe es tiempo lineal, la interpolación entre stops sigue exactamente el punto donde su línea se está dibujando (verificado: runners a ±1.2% del frente del wipe). El avatar (`fs-evo-runner`, mismo `EvoAvatar` de la leyenda con anillo en el color de la serie) responde al atenuado por leyenda (`EvoDim`). El `animation-name` va inline en el elemento y los longhands (10 s / linear / 0.2 s / both) en el CSS scoped. **Segundo ajuste pedido:** al terminar (o detener con ⏹) los avatares NO desaparecen — se quedan en su punto final; en estado `Done` el style inline cambia de `animation-name` a `left`/`top` fijos del último punto, lo que además corta la animación al instante al detener y deja el restart del siguiente replay listo (animation-name none → nombre). El 🏆 lleva `z-index: 3` para quedar sobre el avatar del campeón.


## X5 — Pase visual final (criterio pendiente del doc 13)

- [ ] Con X1–X4 integrados, correr el pase completo de la página (skill `verify`, patrón de los criterios del doc 13): **dark/light mode OK en toda la página; mobile 390px y desktop 1280px OK; 0 errores de consola; sin overflow horizontal.** Al pasar, marcar también el criterio pendiente del doc 13 (línea "Dark/light mode OK…") y el estado de RF8 en su tabla de módulos, apuntando a este doc.

---

## Criterios de aceptación

- [x] **X1:** las stats de sala con ganador-persona se muestran como premios (emoji + nombre cómico + avatar del ganador + dato); las colectivas siguen como tarjetas de stat; tarjetas no computables se auto-ocultan; reveals y count-up funcionando; verificado contra la BD dev que cada premio corresponde a la stat correcta. *(Verificado con Playwright 2026-07-17, admin/sala 1, desktop 1280 light y mobile 390 dark: 11 tarjetas de premio (13 premios contando los dobles) antes de "La sala en números", la grilla colectiva quedó solo con las stats sin persona, 49/49 reveals tras el scroll, count-ups llegando a su valor exacto, 0 errores de consola, sin overflow horizontal. Cotejado con SQL directo: Nostradamus = admin racha 2 ✓, Gafado = admin 6 fallos seguidos ✓, Veleta = empate a 2 cambios resuelto por DisplayName como en el servicio ✓, Rey de Grupos = jugador1 12 pts vs 9 ✓, Rey del KO = admin 11 pts vs 6 ✓, Fantasma = jugador1 83 de 165 ✓. El premio Profeta/Corazones no se renderiza porque la sala 1 no tiene ChampionPredictions — auto-ocultado correcto, misma limitación de datos que RF7.)*
- [x] **X2:** "▶ Revivir el torneo" reproduce el dibujo completo de la gráfica (~8–12 s), los marcadores de cambio de líder aparecen en su momento, el 🏆 remata al final y la gráfica queda en su estado normal; el botón no aparece con `prefers-reduced-motion`; el resaltado por leyenda sigue funcionando. *(Verificado con Playwright 2026-07-17, admin/sala 1, 27/27 checks: wipe de 10 s corriendo y decreciente (muestreado a t≈2 s y t≈6 s), a mitad del replay los marcadores tempranos (`--evo-x` < 40) visibles y los tardíos (> 75) aún ocultos, 🏆 oculto durante y visible al terminar, `fs-evo-done` + `clip-path: none` + botón de vuelta a "▶" al final, segundo replay re-arranca desde cero y "⏹ Detener" salta al estado completo, resaltado por leyenda operando durante y después, botón ausente con `reducedMotion: 'reduce'` (gráfica ya dibujada), mobile 390 dark corriendo el replay sin overflow horizontal, 0 errores de consola en ambos viewports; segundo pase con los runners: 32/32 checks (un avatar por jugador durante el replay, avanzando pegados al frente del dibujo —±1.2% del frente del wipe— y quedándose en su punto final al terminar o detener). El gotcha del `.fs-visible` borrado por el diff de Blazor se detectó precisamente en este pase (screenshot con la tarjeta invisible) y quedó corregido y cubierto por 3 checks de opacidad.)*
- [ ] **X5:** pase visual completo de la página con las secciones nuevas — dark/light, 390px/1280px, 0 errores de consola, sin overflow horizontal — y doc 13 actualizado (criterio visual + estado RF8).
- [ ] Sin migraciones nuevas ni librerías nuevas (solo canvas-confetti y html2canvas ya self-hosted); el gate de visibilidad de RF1 aplica igual a las secciones nuevas (vista previa admin incluida).

---

## Preguntas abiertas

1. **Nombres de los premios (X1):** la tabla de mapeo es propuesta — ¿quieres ajustar/vetar nombres antes de implementar, o se afinan viéndolos en la página?
2. **Rewind con rótulo de partido (X2):** ¿vale la pena el rótulo "partido actual" (implica timer en Blazor) o basta la reproducción visual pura en CSS? Propuesta: empezar sin rótulo y decidir al verla. *(Cerrada 2026-07-17: se implementó y quedó la versión CSS pura sin rótulo — si al verla en vivo se quiere el rótulo, la nota técnica del checklist sigue aplicando.)*
