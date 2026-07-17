# 14 — Extras del Resumen final (RF8) 🎁

**Fecha:** 2026-07-17
**Estado:** 🚧 En implementación — X1 completado (2026-07-17)
**Contexto:** Plan de desarrollo de RF8 del doc 13 (`docs/13_resumenfinal.md`). De los 6 extras propuestos ahí quedan **fuera de alcance** E3 (💬 Muro de despedida) y E6 (📄 PDF de recuerdo) por decisión del 2026-07-17 — con eso la pregunta abierta 2 del doc 13 queda cerrada. Alcance final: **E1 (Quiniela Awards), E2 (Rewind de la tabla), E4 (Bracket: pronóstico vs realidad) y E5 (Trivia del recuerdo)**, más el pase visual final de toda la página (criterio que quedó abierto en el doc 13).

Al no entrar E3, se mantiene la premisa del doc 13: **cero migraciones nuevas** — todo se calcula on-demand desde tablas existentes, y casi todo reusa datos que `FinalSummaryService` ya carga para la página.

---

## Resumen de módulos

| Módulo | Contenido | Depende de | Esfuerzo | Estado |
|--------|-----------|------------|----------|--------|
| X1 | 🏅 Quiniela Awards — stats de sala presentadas como ceremonia de premios | RF3 (hecho) | Bajo (presentación) | ✅ Completado (2026-07-17) |
| X2 | 🎥 Rewind de la tabla — "▶ Revivir el torneo" sobre la gráfica de evolución | RF5 (hecho) | Medio | ⏳ Pendiente |
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

- [ ] **Reusar el mecanismo del wipe de RF5:** el dibujo inicial ya funciona con `clip-path` sobre el SVG completo disparado por `.fs-visible`, y cada marcador aparece cuando el frente pasa por su `--evo-x`. El rewind es el mismo wipe re-disparado con duración larga (~8–12 s vs la del reveal): quitar la clase que deja la gráfica "dibujada", forzar reflow y re-agregarla con una clase modificadora (`fs-evo-replay`) que alarga la `transition-duration`. Sin JS nuevo de animación — solo el toggle (puede ser 100% estado Blazor + CSS, patrón del resaltado de leyenda de RF5).
- [ ] **Indicador de progreso (opcional, decidir en implementación):** un rótulo que muestre el partido "actual" bajo la gráfica durante el replay. Nota técnica: con transiciones CSS puras no hay callback por punto — si se quiere el rótulo, el avance se maneja desde Blazor con un timer (`PeriodicTimer`, un tick por partido) actualizando `clip-path` inline y el rótulo juntos; si no se quiere, basta el CSS. Empezar por la versión CSS y evaluar si el rótulo aporta.
- [ ] **Interacción:** el botón se deshabilita durante la reproducción (o cambia a "⏸/⏹"); el resaltado por leyenda de RF5 sigue funcionando durante y después del replay. Al terminar, la gráfica queda en su estado normal completo.
- [ ] **`prefers-reduced-motion`:** el botón no se muestra (la gráfica ya aparece dibujada de inmediato, coherente con RF5).
- [ ] Se oculta junto con la gráfica cuando `GetEvolutionAsync` devuelve null (<2 partidos con historial).


## X5 — Pase visual final (criterio pendiente del doc 13)

- [ ] Con X1–X4 integrados, correr el pase completo de la página (skill `verify`, patrón de los criterios del doc 13): **dark/light mode OK en toda la página; mobile 390px y desktop 1280px OK; 0 errores de consola; sin overflow horizontal.** Al pasar, marcar también el criterio pendiente del doc 13 (línea "Dark/light mode OK…") y el estado de RF8 en su tabla de módulos, apuntando a este doc.

---

## Criterios de aceptación

- [x] **X1:** las stats de sala con ganador-persona se muestran como premios (emoji + nombre cómico + avatar del ganador + dato); las colectivas siguen como tarjetas de stat; tarjetas no computables se auto-ocultan; reveals y count-up funcionando; verificado contra la BD dev que cada premio corresponde a la stat correcta. *(Verificado con Playwright 2026-07-17, admin/sala 1, desktop 1280 light y mobile 390 dark: 11 tarjetas de premio (13 premios contando los dobles) antes de "La sala en números", la grilla colectiva quedó solo con las stats sin persona, 49/49 reveals tras el scroll, count-ups llegando a su valor exacto, 0 errores de consola, sin overflow horizontal. Cotejado con SQL directo: Nostradamus = admin racha 2 ✓, Gafado = admin 6 fallos seguidos ✓, Veleta = empate a 2 cambios resuelto por DisplayName como en el servicio ✓, Rey de Grupos = jugador1 12 pts vs 9 ✓, Rey del KO = admin 11 pts vs 6 ✓, Fantasma = jugador1 83 de 165 ✓. El premio Profeta/Corazones no se renderiza porque la sala 1 no tiene ChampionPredictions — auto-ocultado correcto, misma limitación de datos que RF7.)*
- [ ] **X2:** "▶ Revivir el torneo" reproduce el dibujo completo de la gráfica (~8–12 s), los marcadores de cambio de líder aparecen en su momento, el 🏆 remata al final y la gráfica queda en su estado normal; el botón no aparece con `prefers-reduced-motion`; el resaltado por leyenda sigue funcionando.
- [ ] **X5:** pase visual completo de la página con las secciones nuevas — dark/light, 390px/1280px, 0 errores de consola, sin overflow horizontal — y doc 13 actualizado (criterio visual + estado RF8).
- [ ] Sin migraciones nuevas ni librerías nuevas (solo canvas-confetti y html2canvas ya self-hosted); el gate de visibilidad de RF1 aplica igual a las secciones nuevas (vista previa admin incluida).

---

## Preguntas abiertas

1. **Nombres de los premios (X1):** la tabla de mapeo es propuesta — ¿quieres ajustar/vetar nombres antes de implementar, o se afinan viéndolos en la página?
2. **Rewind con rótulo de partido (X2):** ¿vale la pena el rótulo "partido actual" (implica timer en Blazor) o basta la reproducción visual pura en CSS? Propuesta: empezar sin rótulo y decidir al verla.
