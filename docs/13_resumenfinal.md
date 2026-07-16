# 13 — Resumen final del torneo 🎉

**Fecha:** 2026-07-16
**Estado:** 🚧 En implementación — RF1, RF2, RF3 y RF4 completados (2026-07-16). Pendiente: RF5–RF8 y confirmar preguntas abiertas.
**Contexto:** Página post-Final por sala (`/pools/{poolId}/final-summary`), el "cierre" del proyecto. Se desbloquea para todos cuando el partido de la Final queda Finalizado; antes de eso solo el admin puede verla (vista previa). Muestra: campeón de la quiniela con podio grande y reveal ceremonial, vitrina definitiva de insignias, stats curiosas de la sala, "Tu participación en números", gráfica de evolución completa, e imagen descargable para compartir en redes. Queda como recuerdo permanente.

---

## Decisiones de producto (confirmadas 2026-07-16)

| Tema | Decisión |
|------|----------|
| Reveal del campeón | **Suspenso animado tipo ceremonia:** al abrir, secuencia 3°… 2°… y explosión de confeti/fuegos artificiales al revelar al 1°. Un botón "Saltar" visible desde el inicio (y la ceremonia solo corre completa la primera visita — flag en `localStorage`, patrón `quiniela_ach_seen_*`; visitas posteriores muestran el podio directo con confeti suave de fondo). |
| Imagen compartible — formato | **Story 9:16 (1080×1920)**, pensada para stories de Instagram/WhatsApp. |
| Imagen compartible — contenido | **Dos variantes:** "Mi participación" (stats personales) y "Podio de la sala" (campeón + top 3). Dos botones de descarga. |
| Stats de visitas (`PageVisitLog`) | **NO se incluyen.** Ese dato hoy es privado del owner (doc 09) y publicarlo rompería esa expectativa. |
| Visibilidad pre-Final | **Gate por datos, no por código:** la página es visible si `la Final está Finalizada` **O** `el usuario es Admin`. Al capturarse el resultado de la Final se desbloquea sola para todos — **cero cambios de código al publicar**. Ver "Estrategia de visibilidad". |
| Alcance | Por sala (como todo el proyecto). Los datos se calculan on-demand desde tablas existentes — **sin migraciones nuevas** (salvo que se elija el extra "Muro de despedida"). |
| Notificación | **N13** "🏆 ¡Tenemos campeón!" al finalizar la Final, con link a la página. Ver módulo RF6. |

---

## Estrategia de visibilidad (respuesta a "¿qué me recomiendas para no hacer cambios al publicar?")

**Recomendación: un solo gate calculado de datos.** Nada de flags de configuración, ni `#if DEBUG`, ni ramas que haya que tocar el día de la Final:

```csharp
// FinalSummaryService
public async Task<bool> IsUnlockedAsync()
{
    await using var db = await dbFactory.CreateDbContextAsync();
    return await db.Matches.AnyAsync(m => m.Stage == MatchStage.Final
                                       && m.Status == MatchStatus.Finalizado);
}
```

- **Página:** si `!unlocked && !isAdmin` → mismo trato que un no-miembro (mensaje "Disponible cuando termine el Mundial 🏆" + link de regreso). Si `!unlocked && isAdmin` → renderiza normal con un banner fijo arriba: *"👁️ Vista previa — solo tú puedes ver esta página hasta que se capture la Final"*.
- **Botón en Pool Detail:** visible para todos solo si `unlocked`; para el admin siempre (con badge "vista previa" si aún no desbloquea). Así los jugadores ni se enteran de que existe hasta el momento correcto.
- **Ventaja clave:** el flujo de publicación es *capturar el resultado de la Final en el panel admin* — exactamente lo que el admin ya iba a hacer. El mismo acto desbloquea la página y dispara la N13. Si se corrige el marcador de la Final después, nada se rompe (el gate sigue cumpliéndose y las stats son on-demand, se auto-corrigen como las insignias).
- **Datos parciales en vista previa:** con la Final sin capturar, podio/stats se calculan con lo finalizado hasta el momento — perfecto para QA visual sin datos sintéticos. El admin verá el "campeón parcial", que es aceptable para preview.

---

## Resumen de módulos

| Módulo | Contenido | Depende de | Estado |
|--------|-----------|------------|--------|
| RF1 | `FinalSummaryService` + gate de visibilidad + botón en Pool Detail | — | ✅ Completado (2026-07-16) |
| RF2 | Página base + hero con reveal ceremonial + fondo de confeti/fuegos | RF1 | ✅ Completado (2026-07-16) |
| RF3 | Secciones con scroll-animations: podio, stats de sala, vitrina de insignias | RF1, RF2 | ✅ Completado (2026-07-16) |
| RF4 | "Tu participación en números" | RF1 | ✅ Completado (2026-07-16) |
| RF5 | Gráfica de evolución completa (multi-línea, todos los jugadores) | RF1 | ⏳ Pendiente |
| RF6 | Notificación **N13** al capturar la Final | RF1 | ⏳ Pendiente |
| RF7 | Imagen compartible 9:16 (2 variantes) + descarga/compartir | RF3, RF4 | ⏳ Pendiente |
| RF8 | Extras seleccionados (ver "Funcionalidad extra") | según elección | ⏳ Pendiente |

---

## RF1 — Servicio y gate

- [x] `FinalSummaryService` nuevo (patrón `IDbContextFactory`, registrado Scoped). Orquesta servicios existentes en vez de duplicar queries: `StandingsService.GetStandingsAsync` (podio/tabla final), `StandingsService.GetPositionHistoryAsync` (evolución, Módulo I), `AchievementsService.GetForPoolAsync` (vitrina, 19 insignias + medallas), `DailyAwardService.GetCountsAsync` (medallero 🌞/🥴), `PlayerStatsService.GetAsync` (base del resumen personal). Las stats curiosas nuevas sí son queries propias del servicio (sobre `Predictions`/`PredictionHistories`/`StandingsSnapshots`/`ChampionPredictions`/`Matches`). *(Implementado con `GetPodiumAsync` sobre StandingsService + ComputePositions tie-aware; los demás servicios se conectan al llegar RF3–RF5.)*
- [x] `IsUnlockedAsync` (ver arriba). Un solo query, cacheable en el ciclo de vida de la página.
- [x] Botón "🎉 Resumen del torneo" en `Pools/Detail.razor` (junto a 📅 Resumen diario / 🎖️ Logros), con la regla de visibilidad descrita. *(Badge "vista previa" para el admin mientras no desbloquea.)*
- [x] Ruta `/pools/{poolId}/final-summary` → `Components/Pages/Pools/FinalSummary.razor(.css)`, `InteractiveServer`, validación de membresía como las demás páginas + `PageVisitLogger` (patrón V3 — pasa a ser la 11ª página instrumentada).

## RF2 — Hero ceremonial + fondo animado

- [x] **Confeti/fuegos:** `canvas-confetti` (MIT, ~11 KB min) **self-hosted** en `wwwroot/lib/` (sin CDN en runtime, consistente con la política de no depender de red para JS funcional; la única CDN actual es flag-icons CSS). Da confeti, cañones laterales y modo fireworks con una sola librería probada. Alternativa sin librería: partículas a mano en canvas (~80 líneas) — se decide en implementación, la API de la página no cambia. *(Decidido: canvas-confetti 1.9.3 en `wwwroot/lib/canvas-confetti/`; el `<script>` de CDN que ya existía en `App.razor` —usado por MatchCard— se cambió al asset local.)*
- [x] Secuencia de reveal (primera visita): pantalla hero navy (invariante al tema, como los sheets) → "3er lugar: …" → "2° lugar: …" → pausa → **1° con explosión de fireworks + ráfagas de confeti** → el confeti decae a una caída suave continua de fondo (baja densidad, `requestAnimationFrame` pausado con `document.hidden`).
- [x] Botón "Saltar ⏭" siempre visible durante la ceremonia. Tras verla una vez (`localStorage: quiniela_final_seen_{poolId}_{userId}` vía `quiniela.getStoredList/setStoredList` existentes), las siguientes visitas van directo al podio. Un botón "🔁 Repetir ceremonia" en el footer para revivirla.
- [x] `prefers-reduced-motion: reduce` → sin ceremonia ni confeti animado (podio directo, confeti estático decorativo en CSS).
- [x] Podio grande estilo Home pero protagonista: avatares (`ProfilePicturePath`), nombre, puntos, delta vs 2° lugar.

## RF3 — Secciones con scroll-animations (estilo Apple)

- [x] Helper JS nuevo en `quiniela.js`: `quiniela.observeReveals(selector)` — un `IntersectionObserver` (threshold ~0.2) que agrega `.fs-visible` a cada `.fs-reveal` al entrar al viewport (una sola vez). Todo el movimiento es CSS (`opacity` + `translateY`/`scale`, `transition-delay` escalonado por `--fs-i` para stagger). Sin librerías de scroll. *(Implementado sin parámetro `selector` — opera sobre `.fs-reveal:not(.fs-observed)`, idempotente; con `prefers-reduced-motion` todo aparece de inmediato.)*
- [x] Efectos por tipo de contenido: números clave con `quiniela.countUp` existente (disparado al hacerse visible), tarjetas de stats con pop escalonado, secciones con headers grandes tipo display (tipografía protagonista, mucho aire — el "sabor Apple" es 80% tipografía y espaciado, 20% animación). *(Count-up vía `<span data-countup="N">` dentro del `.fs-reveal`, mismo easing que `countUp`. El dibujo de la gráfica con `stroke-dasharray` llega con RF5.)*
- [x] Estructura de la página (orden del scroll): Hero/podio → "La sala en números" (stats seleccionadas, una tarjeta grande por stat) → Vitrina de insignias definitiva → Gráfica de evolución → "Tu participación en números" → Compartir (botones de imagen) → footer de despedida. *(Implementadas las secciones de RF3 — stats de sala con las 27 marcadas del catálogo, calculadas en `FinalSummaryService.GetPoolStatsAsync`, y vitrina; cada tarjeta se oculta sola si su stat no es computable aún. Las secciones de RF4/RF5/RF7 se insertarán arriba del footer de despedida al implementarse.)*
- [x] **Vitrina definitiva:** reusa el grid de `Achievements/Index.razor` en modo vitrina (tarjeta por jugador, 19 insignias, medallas 🏅) — idealmente extrayendo el grid a un componente compartido si el markup lo permite sin fricción; si no, se replica el CSS con clases `fs-*`. *(Se replicó el CSS con clases `fs-ach-*` — el scoped CSS de Blazor no permite compartir las clases `ach-*` entre componentes sin extraer también el markup + sheet de detalle, fricción que el doc pedía evitar. Solo insignias obtenidas, con conteo N/19 y medallas 🏅.)*
- [x] Dark mode: tokens `--q-*` en todo; hero navy invariante.

## RF4 — "Tu participación en números"

Sección personal (datos del usuario actual). Ver lista completa en "Catálogo — personal"; propuesta de las **10 por defecto** (las demás opcionales):

1. Posición final (+ "superaste a N jugadores")
2. Puntos totales (+ desglose resultado / instancia / campeón)
3. Aciertos: X de Y (Z%) — vs el promedio de la sala
4. Tu mejor racha 🔥
5. Tu mejor día (fecha + pts) y tu día negro
6. Insignias: N/19 + total de medallas 🏅
7. Tu pick de campeón (bandera) y si acertaste 👑
8. Cambios de pronóstico totales + tu cambio más agónico (min antes del kickoff)
9. Tu equipo talismán (más puntos te dio) y tu equipo maldito (más te falló)
10. Tu mejor posición y tu peor posición en el torneo (+ días como líder, si aplica)

- [x] Todas calculables con `Predictions`/`PredictionHistories`/`StandingsSnapshots`/`ChampionPredictions` — reusa `PlayerStatsService` para lo ya existente y agrega el resto en `FinalSummaryService`. *(Implementado en `FinalSummaryService.GetPersonalStatsAsync(poolId, userId)`: las 10 por defecto + las 8 opcionales marcadas del catálogo personal, en 13 tarjetas (algunas agrupan 2 stats: mejor/peor día, talismán/maldición, paso por la tabla + días líder/podio, grupos vs KO, primer pronóstico + hora habitual). Sección insertada entre la vitrina y el footer, con las mismas clases `fs-stat-card`/`fs-reveal`/count-up de RF3. Posición tie-aware (`ComputePositions`, consistente con el hero); promedio de la sala calculado solo sobre partidos finalizados; tarjetas de lobo solitario/penales/pick de campeón se ocultan si no aplican. Los "días como líder / en podio" se cuentan por posición al cierre de cada día local CDMX con snapshot.)*

## RF5 — Gráfica de evolución completa

- [ ] Un solo SVG multi-línea con **todos** los jugadores (a diferencia del Módulo I, que era un mini-gráfico por jugador): eje Y = posición (invertido), eje X = partidos finalizados en orden de `KickoffUtc`. Reusa `GetPositionHistoryAsync` tal cual.
- [ ] Color por jugador (paleta fija de ~8, la sala real tiene 4 miembros), leyenda con avatar+nombre; tocar la leyenda resalta esa línea y atenúa las demás. La línea del campeón más gruesa y con remate 🏆 en el último punto.
- [ ] Animación de dibujo al entrar al viewport (RF3). Densidad: 104 partidos ≈ 104 puntos por línea — se dibujan las líneas sin marcador por punto (solo el último), con puntos visibles únicamente en hitos (cambios de líder) para no saturar.

## RF6 — Notificación N13 (¡Tenemos campeón!)

- [ ] En `ScoringService.RecalculateForMatchAsync`, tras `ResolveChampionAsync` (el hook `Stage == Final` ya existe, `ScoringService.cs:47-48`): por cada pool con miembros, enviar push a todos vía `PushNotificationService`:
  - Título: `🏆 ¡Tenemos campeón!`
  - Cuerpo: `{DisplayName} ganó la quiniela {PoolName} 🎉 Mira el resumen final del torneo` (el ganador recibe una variante personal: `¡GANASTE la quiniela {PoolName}! 👑🎉`)
  - Link: `/pools/{poolId}/final-summary`
- [ ] **Dedup:** `NotificationLog` `Type="FinalSummary"`, `MatchId` = la Final (índice único existente `(UserId, MatchId, Type)` lo resuelve solo, patrón N9/N10). **Decisión propuesta:** si el admin *corrige* el marcador de la Final después, NO se re-notifica (el dedup lo impide) — evita la tormenta de pushes por correcciones; si la corrección cambiara al campeón de la quiniela, el admin puede avisar por el chat del grupo. ⚠️ Confirmar.
- [ ] N13 se manda **después** de la N1 normal del partido (que ya informa "acertaste/fallaste" la Final) — son mensajes distintos y ambos valen.

## RF7 — Imagen compartible

### Opciones evaluadas (respuesta a "¿es posible? ¿qué alternativas hay?")

Sí es posible. Opciones, de la recomendada a la menos:

| # | Opción | Cómo funciona | Pros | Contras |
|---|--------|--------------|------|---------|
| 1 | **`html2canvas` self-hosted (cliente)** ⭐ | Se maqueta un nodo DOM oculto de 1080×1920 con CSS "capture-safe"; la librería lo re-renderiza a un `<canvas>` → `toBlob()` → PNG | Cero carga/costo en servidor; la tarjeta se diseña con HTML/CSS normal (rápido de iterar); emoji y fuentes se ven como en el dispositivo | No soporta todo CSS (`backdrop-filter`, algunos gradientes complejos); imágenes remotas requieren CORS — las banderas actuales vienen del CSS de flag-icons en jsdelivr (sí manda `Access-Control-Allow-Origin: *`, pero mejor no depender: en la tarjeta usar emoji de bandera 🇦🇷 o assets locales) |
| 2 | `modern-screenshot` / `html-to-image` (cliente) | Igual que 1 pero vía SVG `foreignObject` — fidelidad CSS casi perfecta | Mejor soporte CSS que html2canvas | Bugs conocidos de fuentes/imágenes en Safari iOS — y los amigos van a compartir *desde el teléfono*, Safari/WebView es el caso principal |
| 3 | Canvas 2D dibujado a mano (cliente, sin librería) | JS dibuja texto/formas/avatares directo en un canvas 1080×1920 | Control absoluto, cero sorpresas, cero dependencias | El layout es código imperativo (cada cambio de diseño = tocar JS); wrapping de texto manual |
| 4 | Servidor: ImageSharp/SkiaSharp | Endpoint que compone el PNG en C# | Pixel-perfect idéntico para todos, funciona sin JS | Emoji/banderas/fuentes en el servidor (Azure Linux) son dolorosas de renderizar; más código; carga al servidor |
| 5 | Servidor: screenshot headless (Playwright) | Una ruta oculta renderiza la tarjeta, el server la fotografía | Fidelidad perfecta | Chromium en el App Service = dependencia pesada, arranque frío, overkill para ~4 usuarios |

**Recomendación: opción 1** (con la 3 como fallback si html2canvas diera guerra con algo puntual):

- [ ] Componente `ShareCard.razor` — nodo oculto (`position:fixed; left:-9999px`) de 1080×1920 con diseño propio para story: fondo navy con confeti decorativo **CSS/inline** (no el canvas animado), tipografía grande, marca "Quiniela Mundial 2026 · {PoolName}". CSS restringido a lo que html2canvas soporta bien (flex/grid, gradientes lineales simples, border-radius, sombras). Banderas como **emoji** (🇲🇽🇫🇷) y avatares locales (`ProfilePicturePath` es local, sin problema de CORS).
- [ ] Dos variantes (mismo componente, parámetro): **"Mi participación"** (posición, pts, %, racha, insignias, campeón pick) y **"Podio de la sala"** (top 3 con avatares + campeón destacado).
- [ ] `quiniela.downloadShareCard(elementId, fileName)` en `quiniela.js`: `html2canvas(el, {scale:1}) → canvas.toBlob → `
  - **Móvil con Web Share API nivel 2** (`navigator.canShare({files})`): `navigator.share({files:[png]})` — abre el share sheet nativo y va directo a Instagram/WhatsApp. Es la vía principal de compartir.
  - **Fallback/desktop:** `<a download="quiniela-2026.png">`.
- [ ] `html2canvas.min.js` self-hosted en `wwwroot/lib/`, cargado **lazy** (solo al pulsar descargar, `import()` dinámico) para no engordar la carga inicial.

## RF8 — Extras (según selección, ver "Funcionalidad extra")

---

## Catálogo de stats curiosas de la sala

Ordenadas por **impacto/comicidad** (criterio: qué tanto van a cagarse de risa o a presumirlo en el grupo de WhatsApp). Todas calculables con datos existentes; la columna "Fuente" lo demuestra. **Marca con `[x]` las que quieres** — recomiendo **8–12** para que el scroll tenga ritmo y no se diluya (las Tier S completas + tus favoritas de Tier A).

### Tier S — Las que van a dar de qué hablar

| # | Elegir | Stat | Qué muestra | Fuente |
|---|:---:|------|-------------|--------|
| 1 | [x] | 💀 **El engaño colectivo** | El partido donde TODOS fallaron ("México 1-3 Inglaterra: 4 de 4 lo fallaron") | `Predictions.PtsResult` por partido |
| 2 | [x] | 🔥 **La racha del torneo** | La mejor racha de aciertos consecutivos de la sala (jugador + número + en qué partidos fue) | orden por `KickoffUtc` (lógica `BestStreak` existente) |
| 3 | [x] | 🐺 **El acierto más solitario** | El momento lone-wolf más dramático: único en acertar cuando todos los demás fallaron | `Predictions` agrupadas por match (lógica LG3 existente) |
| 4 | [x] | 🚀 **La remontada** | La mayor subida de posiciones del torneo (de 4° a 1° entre el partido X y el Y) | `StandingsSnapshots` |
| 5 | [ ] | 📉 **La caída libre** | La mayor caída (contraparte cómica de la anterior) | `StandingsSnapshots` |
| 6 | [x] | ⏰ **El cambio más agónico** | El cambio de pronóstico más cercano al kickoff de todo el torneo (jugador, partido, minutos exactos… ¿y le atinó?) | `PredictionHistories.ChangedAt` vs `KickoffUtc` |
| 7 | [x] | 🎰 **El más indeciso** | Quién cambió más veces sus pronósticos en total + el partido que más cambios provocó en toda la sala | `PredictionHistories` (filas−1 por predicción) |
| 8 | [x] | 🥶 **La peor racha** | Más fallos consecutivos (jugador + número) | inverso de la #2 |
| 9 | [x] | 🪦 **El equipo maldito** | El equipo que más puntos le costó a la sala (más pronósticos fallados en sus partidos) | `Predictions` × `Matches` por equipo |
| 10 | [x] | 🧲 **El equipo talismán** | El equipo que más puntos regaló a la sala | contraparte de la #9 |
| 11 | [x] | 📅 **El día dorado / el día negro** | El día que la sala más puntos sumó vs el día de peor % colectivo | `Predictions` agrupadas por fecha local CDMX (patrón `DailyAwardService`) |
| 12 | [x] | 🤯 **La sorpresa del torneo** | El resultado que nadie de la sala vio venir (todos pronosticaron lo mismo… y salió lo contrario) | `PredOutcome` unánime vs resultado |

### Tier A — Sólidas, completan la narrativa

| # | Elegir | Stat | Qué muestra | Fuente |
|---|:---:|------|-------------|--------|
| 13 | [x] | 🏆 **El partido que decidió todo** | Desde qué partido el campeón tomó el 1° y ya no lo soltó + margen final vs el 2° | `StandingsSnapshots` |
| 14 | [x] | 🔀 **La guerra por la cima** | Cuántas veces cambió el líder de la tabla durante el torneo | `StandingsSnapshots` |
| 15 | [x] | ⚖️ **Alergia al empate** | % de empates reales del mundial vs % que la sala se atrevió a pronosticar | `PredOutcome=='D'` vs resultados |
| 16 | [ ] | 🎯 **La sala vs el chango** | % de aciertos global de la sala vs el 33% del azar ("le ganamos a un chango lanzando dardos por 12 puntos") | agregado global |
| 17 | [x] | 🙈 **Nadie lo vio venir** | Cuántos partidos terminaron en un resultado que NADIE pronosticó | `Predictions` por match |
| 18 | [x] | 🤝 **El partido obvio** | Los partidos donde el 100% de la sala acertó | inverso de la #1 |
| 19 | [x] | 🥅 **Los que olieron los penales** | Cuántos partidos se fueron a penales y quiénes lo profetizaron | `PredInstance`/`DecidedIn` (lógica penalty-prophet) |
| 20 | [x] | 💸 **Puntos dejados en la mesa** | Puntos perdidos por partidos sin pronosticar (por jugador; "el fantasma" = quien más dejó ir) | `PoolMembers` × `Matches` − `Predictions` |
| 21 | [x] | 🌞🥴 **Medallero del día a día** | Quién acumuló más "Mejor del día" y quién más "Peor del día" | `DailyAwardService.GetCountsAsync` (existente) |
| 22 | [x] | 👑 **Fe en el campeón** | Los picks de campeón de todos (banderas), quién acertó… y los 💔 corazones rotos con la fecha en que su equipo murió | `ChampionPredictions` + lógica heartbroken |
| 23 | [x] | 🕐 **Rey de grupos vs rey del KO** | Quién ganó cada "mitad" del torneo (pts en grupos vs pts en eliminatorias) | `Points` split por `Stage` |

### Tier B — Curiosas, para rellenar si se quiere más

| # | Elegir | Stat | Qué muestra | Fuente |
|---|:---:|------|-------------|--------|
| 24 | [x] | 🌙 **El búho** | El pronóstico capturado a la hora más rara (las 3:47 AM…) + a qué hora suele pronosticar cada quién | `PredictionHistories.ChangedAt` (hora local CDMX) |
| 25 | [x] | ⚡ **El madrugador vs el del último minuto** | Anticipación promedio al kickoff por jugador (quién pronostica con días vs con minutos) | `ChangedAt` vs `KickoffUtc` |
| 26 | [x] | 🗿 **Los inamovibles** | % de pronósticos de la sala que jamás se cambiaron | `PredictionHistories` |
| 27 | [ ] | 🆚 **El duelo más parejo** | La pareja de jugadores con head-to-head más cerrado | `HeadToHeadService` (existente) |
| 28 | [x] | 🗺️ **El estadio de la suerte** | La sede donde la sala tuvo su mejor % de aciertos | `Match.Venue` |
| 29 | [x] | 📊 **El más constante** | Quien menos se movió de posición en todo el torneo ("vivió el mundial entero en 3°") | varianza de `StandingsSnapshots` |
| 30 | [x] | 🔢 **La sala en números totales** | Cierre tipo Spotify Wrapped: N pronósticos, N cambios de opinión, N partidos, N goles reales, N insignias repartidas, N días de torneo | agregados simples |

> **Nota sobre #22/heartbroken y campeón:** los cambios de pick de campeón NO tienen historial (`ChampionPrediction` es upsert sin tabla de history), así que stats tipo "cambió de campeón 3 veces" **no son posibles** con los datos actuales — solo el pick final.

---

## Catálogo — personal ("Tu participación en números")

Las 10 por defecto están en RF4. Opcionales adicionales, mismo criterio de marcar:

| Elegir | Stat personal |
|:---:|------|
| [x] | 🐺 Tus momentos lobo solitario ("fuiste el único en acertar Brasil–Noruega") |
| [x] | ⚖️ Empates acertados (los más difíciles de atinar) |
| [x] | 🥅 Tus profecías de penales cumplidas |
| [x] | 📅 % de partidos pronosticados (compromiso: 100% = nunca dejaste uno pasar) |
| [x] | 🕐 Tu fase fuerte: grupos vs eliminatorias |
| [x] | 🌙 Tu hora habitual de pronosticar |
| [x] | 📈 Días en el podio / días como líder |
| [x] | 🗓️ Tu primer pronóstico (fecha exacta, "llevas N días en esto") |

---

## Funcionalidad extra propuesta (respuesta a "¿qué más se puede añadir?")

Ordenadas por relación valor/esfuerzo:

| # | Elegir | Extra | Descripción | Esfuerzo |
|---|:---:|------|-------------|----------|
| E1 | [x] | 🏅 **Los Quiniela Awards** ⭐ | En vez de (o además de) tarjetas de stats sueltas, presentarlas como **ceremonia de premios con nombres cómicos**: "El Nostradamus" (mejor racha), "El Veleta" (más indeciso), "El Kamikaze" (cambio más agónico), "El Ancla" (peor del día más veces)… Es la MISMA data de las stats, solo cambia la presentación a "premio + ganador + dato". Encaja perfecto con el reveal ceremonial. | Bajo (es presentación) |
| E2 | [x] | 🎥 **Rewind de la tabla** | Botón "▶ Revivir el torneo": la gráfica de evolución se reproduce sola partido a partido como animación (las líneas avanzan, el liderato cambia, se marca cada cambio de líder). Factor wow alto. | Medio (JS/SVG, data ya existe) |
| E3 | [x] | 💬 **Muro de despedida** | Cada miembro deja un mensaje final ("GG", "el año que viene los destrozo") que queda para siempre en la página. **Única propuesta que requiere tabla nueva** (`FinalMessage`: UserId/PoolId/Text/CreatedAt + migración). Alto valor emocional para el "recuerdo". | Medio (tabla + CRUD mínimo) |
| E4 | [x] | 📊 **Bracket: pronóstico vs realidad** | Comparación visual del bracket real vs lo que cada quien pronosticó en KO (reusa componentes de Bracket). | Medio |
| E5 | [x] | 🔮 **Trivia del recuerdo** | Mini-quiz sobre la propia quiniela ("¿quién fue el único en acertar X?") con las respuestas saliendo de las stats. Divertido pero efímero. | Medio-alto |
| E6 | [x] | 📄 **PDF de recuerdo** | Export completo de la página a PDF. Con la imagen 9:16 ya cubierto el caso principal de compartir; el PDF añade poco. | Alto (no recomendado) |

**Recomendación: E1 sí o sí** (convierte las stats en el momento más divertido de la página gratis), **E2 si quieres el wow**, **E3 si te gusta la idea del recuerdo colectivo** (única con migración).

---

## Criterios de aceptación

- [x] Antes de la Final: jugadores no-admin no ven el botón en Pool Detail y la ruta directa les muestra "disponible al terminar el Mundial"; el admin ve la página completa con banner de vista previa. *(Verificado en navegador real con Playwright, 2026-07-16.)*
- [ ] Al capturar el resultado de la Final en el admin: la página se desbloquea para todos SIN deploy ni cambio de código, y cada miembro con suscripción push recibe la N13 con link directo.
- [x] Primera visita: ceremonia 3°→2°→1° con confeti/fireworks; "Saltar" funciona; visitas siguientes van directo al podio; "Repetir ceremonia" disponible; `prefers-reduced-motion` respetado. *(Verificado en navegador real con Playwright, 2026-07-16.)*
- [x] Todas las secciones hacen reveal al scroll (IntersectionObserver + CSS), números con count-up, gráfica se dibuja al entrar al viewport. Sin librerías de scroll/animación (solo canvas-confetti y html2canvas, ambas self-hosted). *(Verificado con Playwright 2026-07-16: 27 reveals, 0 visibles fuera de viewport antes del scroll y 27/27 tras el scroll, 13 count-ups llegando a su valor exacto, `prefers-reduced-motion` muestra todo sin animar. La parte de "gráfica se dibuja al entrar al viewport" queda pendiente hasta RF5 — la sección de gráfica aún no existe.)*
- [ ] Gráfica de evolución: todos los jugadores en un solo chart, leyenda interactiva, línea del campeón destacada.
- [x] Vitrina: 19 insignias + medallas por jugador, mismo tratamiento visual que Achievements. *(Verificado en navegador real 2026-07-16: tarjeta por jugador ordenada por insignias, celdas con color por categoría, filas de medallas 🏅, conteo N/19, dark/light OK.)*
- [x] "Tu participación en números" muestra las stats personales acordadas, correctas contra la BD. *(Verificado en navegador real con Playwright 2026-07-16: 13 tarjetas renderizadas, 14/14 reveals al scroll, 7 count-ups llegando a su valor exacto, 0 errores de consola, light 1280px y dark 390px sin overflow. Cifras cotejadas contra la BD dev con SQL directo: puntos 20 = 18 resultado + 2 instancia + 0 campeón ✓, aciertos 6/13 vs promedio de sala 40% (10/25) ✓, 13 de 95 partidos pronosticados ✓, grupos 9 pts vs KO 11 pts ✓, 0 cambios de pronóstico ✓, primer pronóstico 9-jul 10:01 a.m. CDMX ✓. El gate no-admin sigue intacto.)*
- [ ] Descargar imagen: ambas variantes generan PNG 1080×1920 legible (banderas emoji, avatar, sin elementos cortados); en móvil `navigator.share` abre el share sheet con la imagen; en desktop descarga directa.
- [ ] Corrección posterior del marcador de la Final: la página se auto-corrige (on-demand), sin re-notificación N13.
- [ ] Dark/light mode OK en toda la página; mobile 390px y desktop 1280px OK; 0 errores de consola.
- [x] Se agrega `PageVisitLogger` a la página (aparece en el Log del owner). *(PageName "Resumen final"; filas de prueba borradas de la BD dev.)*

---

## Preguntas abiertas (para cerrar antes de implementar)

1. **Selección de stats:** marca las `[ ]` del catálogo (sala + personal). Recomendación: Tier S completo (12) o Tier S + 3-4 de Tier A si va con formato Awards (E1).
2. **Extras:** ¿E1/E2/E3? (E3 es la única con migración).
3. **N13 y correcciones:** ¿confirmas que corregir el marcador de la Final NO re-notifica (dedup duro)?
4. **¿La imagen "Podio de la sala" incluye los avatares reales de los 3?** (fotos de tus amigos saliendo a redes sociales — asumo que sí porque es un grupo de confianza, pero lo señalo).
5. **Tercer lugar del mundial:** el partido de TercerLugar se juega ANTES de la Final (18-jul vs 19-jul). El gate propuesto (solo `Stage == Final`) lo ignora — está bien, pero confírmalo: la página se desbloquea con la Final aunque el 3er lugar ya se haya jugado.
6. **Título/hero:** ¿algo tipo "Mundial 2026 · {PoolName}" o quieres un nombre propio para la página ("El Gran Cierre", "Wrapped 2026")?
