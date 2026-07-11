# 10 — Insignias de grupos/KO (segunda tanda)

**Fecha:** 2026-07-10
**Estado:** ✅ Implementado (2026-07-10) — LG1/LG2/LG3/N12 completos. `CheckAnnouncementsAsync` se generalizó a una lista de anuncios `(Type, ExpiresUtc, Title, Body)` (la opción limpia de las dos propuestas); expiración de N12: 2026-07-24.
**Contexto:** El catálogo actual tiene 13 insignias (`AchievementsService`). Este documento define **6 insignias nuevas** calculables on-demand con los datos existentes (`Predictions`, `Matches`, `ChampionPredictions`) — sin tablas ni columnas nuevas, mismo patrón que el resto — y la notificación **N12** (anuncio one-shot) que las presenta a los usuarios.

Las 6 nuevas:

| Icono | Insignia | Categoría | Medallas |
|:-----:|----------|-----------|:--------:|
| 🧠 | Dueño del Grupo | Positiva | No |
| 🔮 | Profeta de Penales | Prestigio | 🏅 por partido |
| 💔 | Corazón Roto | Irónica | No |
| 🐺 | Lobo Solitario | Positiva | 🏅 por partido |
| 🐑 | Oveja Negra | Irónica | 🏅 por partido |
| 🙅 | El Optimista | Irónica | No |

---

## Decisiones de producto

| Tema | Decisión |
|------|----------|
| Persistencia | **On-demand, sin tabla nueva.** Todo se recalcula desde `Predictions`/`Matches`/`ChampionPredictions` en `GetForPoolAsync` — se auto-corrige si el admin corrige un marcador viejo (mismo principio que las 13 existentes). |
| Medallas | **Lobo Solitario**, **Oveja Negra** y **Profeta de Penales** llevan conteo de medallas 🏅 (una por partido en que ocurrió), reusando el mecanismo `EarnedBadge.Medals` de mejor/peor del día. Las otras 3 son binarias. |
| Corazón Roto | Ajuste sobre la propuesta original ("eliminado en fase de grupos"): se otorga en cuanto el equipo pronosticado como campeón **queda eliminado del mundial**, en cualquier fase (grupos o KO). |
| Lobo/Oveja — partidos que cuentan | Solo partidos donde **al menos 3 miembros de la sala pronosticaron** (el propio + 2 más). Evita que "acerté porque fui el único que jugó" cuente como hazaña, y que "todos los demás acertaron" se cumpla con un solo rival. |
| Lobo/Oveja — no pronosticar | Para **Lobo**: los miembros sin pronóstico cuentan como fallo (basta que nadie más tenga `PtsResult > 0`). Para **Oveja**: el usuario debe haber **pronosticado y fallado** (no pronosticar no da la insignia — la burla es fallar activamente), y "todos los demás" se evalúa solo sobre los miembros que sí pronosticaron. |
| Notificación de desbloqueo | Las 6 claves nuevas entran al flujo normal de **N4** ("¡Insignia desbloqueada!") sin exclusión — a diferencia de `daily-best`/`daily-worst`, no dependen de un cierre de día. Ver "Interacción con N4". |
| Anuncio de lanzamiento | **N12**, one-shot al publicar en Azure, calcado de N11 (`Announcement:` + fecha de expiración). |

---

## Catálogo

```csharp
// AchievementCatalog.All — 6 entradas nuevas
new("group-master",    "🧠", "Dueño del Grupo",
    "Acertó el resultado de los 6 partidos de un mismo grupo",
    AchievementCategory.Positive),
new("penalty-prophet", "🔮", "Profeta de Penales",
    "Pronosticó que un partido de eliminatorias se definiría en penales… y así fue. Una medalla 🏅 por cada profecía cumplida.",
    AchievementCategory.Prestige),
new("heartbroken",     "💔", "Corazón Roto",
    "El equipo que eligió como campeón ya fue eliminado del mundial",
    AchievementCategory.Ironic),
new("lone-wolf",       "🐺", "Lobo Solitario",
    "Fue el único de la sala en acertar un partido. Una medalla 🏅 por cada hazaña.",
    AchievementCategory.Positive),
new("black-sheep",     "🐑", "Oveja Negra",
    "Fue el único de la sala en fallar un partido que todos los demás acertaron. Una medalla 🏅 por cada resbalón.",
    AchievementCategory.Ironic),
new("optimist",        "🙅", "El Optimista",
    "Nunca pronosticó un empate en fase de grupos, con al menos 10 pronósticos de grupos hechos",
    AchievementCategory.Ironic),
```

---

## Resumen de módulos

| # | Hecho | Módulo | Esfuerzo |
|---|:-----:|--------|----------|
| LG1 | [x] | Insignias de cálculo directo: 🧠 Dueño del Grupo, 🔮 Profeta de Penales, 🙅 El Optimista | ~2 h |
| LG2 | [x] | 💔 Corazón Roto (regla de eliminación) | ~2 h |
| LG3 | [x] | 🐺 Lobo Solitario / 🐑 Oveja Negra con medallas 🏅 | ~2 h |
| N12 | [x] | Anuncio one-shot: nuevas insignias | ~1 h |

**Orden sugerido:** LG1 → LG2 → LG3 → N12 (N12 al final, se publica junto con el deploy que incluye las insignias). Los tres módulos LG son independientes entre sí; solo comparten el alta en `AchievementCatalog`.

---

## Módulo LG1 — Insignias de cálculo directo

### 🧠 Dueño del Grupo — *Positiva*

**"Acertó el resultado de los 6 partidos de un mismo grupo."**

Es el equivalente en fase de grupos del 🎯 Francotirador KO (que solo premia eliminatorias).

**Cálculo** (en `GetForPoolAsync`, reusando `orderedPredictions` que ya trae `Match` incluido):

1. Filtrar las predicciones del usuario con `Match.Stage == Grupos` y `Match.GroupCode != null` (partido ya `Finalizado` — el filtro global ya lo garantiza).
2. Agrupar por `Match.GroupCode`.
3. Se otorga si **algún grupo** cumple: el usuario tiene predicción en los **6** partidos del grupo y **todas** con `PtsResult > 0`.

Notas:

- Se exige el grupo **completo finalizado** implícitamente: con menos de 6 partidos finalizados no puede haber 6 predicciones acertadas de ese grupo.
- El conteo es contra 6 fijo (formato 2026: 12 grupos de 4 equipos, 6 partidos por grupo), no contra "los partidos que pronosticó" — pronosticar 4 y acertar 4 no cuenta.

**Criterios de aceptación**

- [x] Usuario con los 6 partidos del Grupo A pronosticados y acertados obtiene la insignia
- [x] Usuario con 5/6 acertados y el sexto fallado o sin pronóstico **no** la obtiene
- [x] Basta un grupo perfecto aunque los demás tengan fallos

### 🔮 Profeta de Penales — *Prestigio*

**"Pronosticó que un partido de eliminatorias se definiría en penales… y así fue."** Una medalla 🏅 por cada partido en que ocurrió (mismo mecanismo `EarnedBadge.Medals` que 🐺/🐑, ver LG3).

Aprovecha `PredInstance`/`DecidedIn`, que hoy solo alimentan el bono KO (`PtsInstance`) y ningún logro explota.

**Cálculo:** se cuentan las predicciones del usuario con:

- `Match.Stage != Grupos`, partido `Finalizado`
- `PredInstance == MatchDecidedIn.Penales`
- `Match.DecidedIn == MatchDecidedIn.Penales`

`Medals` = ese conteo; la insignia se otorga con conteo ≥ 1.

(No hace falta mirar `PtsInstance`: acierto de instancia = `PredInstance == DecidedIn`, y así queda insensible a la configuración de `PtsBonusKO` de la sala.)

**Criterios de aceptación**

- [x] Pronosticó penales y el partido se fue a penales → insignia con 1 medalla 🏅 (aunque haya fallado el equipo que avanza)
- [x] Acierta penales en 3 partidos distintos → 3 medallas 🏅
- [x] Pronosticó penales y el partido se definió en 90'/prórroga → no
- [x] Pronosticó 90' o prórroga → no, aunque acierte la instancia

### 🙅 El Optimista — *Irónica*

**"Nunca pronosticó un empate en fase de grupos, con al menos 10 pronósticos de grupos hechos."**

Siempre le apostó a un ganador — los empates no existen en su mundo (y el mundial siempre cobra unos cuantos).

**Cálculo** (sobre las predicciones de grupos con partido `Finalizado`):

- `GroupPredictions >= 10` (dato ya disponible en `PlayerStats`)
- Ninguna con `PredOutcome == 'D'`

Se limita a fase de grupos porque en KO el empate no es un desenlace pronosticable (ahí se pronostica avance + instancia).

Notas:

- Como toda insignia on-demand, **se puede perder**: si en la jornada siguiente pronostica un empate, desaparece de la vitrina (mismo comportamiento que 🗿 Dicho y Hecho o 🐢 Modo tortuga). La notificación N4 ya enviada no se retracta — la vitrina es la verdad.
- No exige haber **fallado** los empates: la ironía es la fe ciega, no el castigo.

**Criterios de aceptación**

- [x] 10+ pronósticos de grupos finalizados, cero `'D'` → insignia
- [x] 9 pronósticos sin empates → no (aún no llega al mínimo)
- [x] Un solo pronóstico de empate en grupos la anula, aunque tenga 40 sin empate

---

## Módulo LG2 — 💔 Corazón Roto

### Objetivo

**"El equipo que eligió como campeón ya fue eliminado del mundial."**

Completa el triángulo con 👑 Vidente (acertó campeón) y 🪦 El Traidor (apostó contra el campeón): este es la fe mal invertida. A diferencia de aquellas, **no espera a la Final** — se desbloquea en el momento en que el equipo queda fuera.

### Regla de eliminación

El equipo `ChampionPredictions.TeamId` del usuario (en esa sala) está **eliminado** si se cumple cualquiera de las dos:

1. **Eliminado en KO:** perdió un partido KO `Finalizado` (cualquier etapa excepto `TercerLugar` — ese partido no elimina a nadie del título: sus dos equipos ya cayeron en semifinales). Perdedor = el equipo con menor marcador (`HomeScore`/`AwayScore` guardan el resultado definitivo; mismo criterio que `GetRealChampionTeamIdAsync`).
2. **Eliminado en grupos:** todos los partidos de `Stage == Grupos` están `Finalizado`, **todos** los cruces de `Dieciseisavos` tienen `HomeTeamId` y `AwayTeamId` asignados, y el equipo no aparece en ninguno.

La condición doble de la regla 2 evita falsos positivos a media asignación de cruces: mientras el admin no haya llenado los 16avos completos, nadie recibe la insignia por esa vía.

### Implementación

- Nuevo helper en `AchievementsService`, p. ej. `GetEliminatedTeamIdsAsync(db)`, que devuelve el set de equipos eliminados **una vez por sala** (no por usuario); el loop de miembros solo consulta `eliminated.Contains(championTeamId)` con el `TeamId` de `championPoints`… que hoy solo trae `Points` — ampliar esa consulta para traer también `TeamId`.
- Es **irreversible en la práctica** (un eliminado no vuelve), salvo corrección de marcador del admin: on-demand se auto-corrige, y la fila de `NotifiedAchievements` ya registrada solo evita re-notificar (comportamiento existente para todas las insignias).
- **Timing de N4:** la eliminación por regla 2 se materializa cuando el admin asigna cruces (`KnockoutService`), pero N4 solo corre al capturar resultados (`ScoringService`). Se acepta: la vitrina (on-demand) la muestra de inmediato y la notificación llega con el siguiente resultado capturado.

### Criterios de aceptación

- [x] Su campeón pierde en octavos (resultado capturado) → insignia desde ese momento
- [x] Su campeón no avanzó de grupos → insignia cuando grupos terminó **y** los 16avos están completos
- [x] Campeón pronosticado aún vivo → no aparece
- [x] Usuario sin `ChampionPrediction` en la sala → no aplica
- [x] Perder la final **sí** otorga la insignia (subcampeón = eliminado del título); ganar el 3er lugar no la quita

---

## Módulo LG3 — 🐺 Lobo Solitario / 🐑 Oveja Negra (con medallas 🏅)

### Objetivo

Dos caras del mismo cálculo por partido, con **una medalla 🏅 por cada partido** en que ocurrió (mismo render de medallas que 🌞/🥴, `EarnedBadge.Medals`):

- **🐺 Lobo Solitario** — *Positiva*: "Fue el único de la sala en acertar un partido."
- **🐑 Oveja Negra** — *Irónica*: "Fue el único de la sala en fallar un partido que todos los demás acertaron."

### Regla (por partido `Finalizado`, cualquier fase, por sala)

Solo se evalúan partidos con **≥ 3 predicciones** en la sala (ver Decisiones).

| | 🐺 Lobo Solitario | 🐑 Oveja Negra |
|---|---|---|
| El usuario | `PtsResult > 0` | Pronosticó y `PtsResult == 0` |
| Los demás | **Nadie más** de la sala tiene `PtsResult > 0` (sin pronóstico cuenta como fallo) | **Todos los demás que pronosticaron** (≥ 2) tienen `PtsResult > 0` |

Un mismo partido nunca produce lobo y oveja a la vez: el lobo exige que nadie más acertara y la oveja exige que todos los demás acertaran — con ≥ 3 pronósticos son condiciones incompatibles. Un usuario acumula una medalla por cada partido que cumpla su lado.

### Implementación

- En `GetForPoolAsync` ya se cargan todas las predicciones de la sala con partido finalizado (`orderedPredictions`); basta reagruparlas **por `MatchId`** y contar por usuario los partidos-lobo y partidos-oveja antes del loop de miembros (un solo pase, sin consultas nuevas).
- `Medals` = conteo respectivo; la insignia se otorga con conteo ≥ 1. Render de medallas y bottom sheet: sin cambios, reusa lo hecho en M3 (`08_insignias_mejorpeor.md`).

### Criterios de aceptación

- [x] Partido con 4 pronósticos donde solo uno acertó → ese usuario suma 1 medalla de lobo
- [x] Partido con 4 pronósticos donde solo uno falló → ese usuario suma 1 medalla de oveja
- [x] Partido con 2 pronósticos no cuenta para ninguno de los dos lados
- [x] Miembro sin pronóstico: no puede ser oveja, no bloquea la oveja de otro (solo cuentan los que pronosticaron) y cuenta como fallo para efectos del lobo
- [x] Las medallas se muestran una 🏅 por partido, con wrap, igual que mejor/peor del día
- [x] Corregir un marcador viejo ajusta los conteos en la siguiente visita (on-demand)

---

## Interacción con N4 (insignia desbloqueada)

Las 6 claves nuevas **no se excluyen** de `ScoringService.NotifyNewAchievementsAsync`: al capturarse el resultado que las desbloquea, el usuario recibe el push estándar "🎖️ ¡Insignia desbloqueada!" (a diferencia de `daily-best`/`daily-worst`, que se comunican solo vía N10 porque dependen del cierre del día).

Limitaciones aceptadas (comportamiento existente de N4, sin cambios):

- **Medallas adicionales no re-notifican**: la clave `lone-wolf`/`black-sheep`/`penalty-prophet` ya queda en `NotifiedAchievements` con la primera medalla; las siguientes solo suman en la vitrina (misma limitación registrada en M3 para la animación de desbloqueo).
- **Insignias perdibles** (🙅 El Optimista): la notificación no se retracta si luego se pierde — la vitrina es la verdad.
- **💔 Corazón Roto por vía de grupos** puede notificarse con retraso (ver LG2, timing de N4).

---

## Módulo N12 — Anuncio one-shot: nuevas insignias

*Extiende la serie N0–N11 de `07_notificaciones.md`. Calcado de N11.*

### Objetivo

Anunciar **una sola vez** a todos los usuarios suscritos que hay 6 insignias nuevas en la vitrina, al momento de publicar en Azure el release que las incluye. Notificación de hype, no ligada a partido ni sala (`MatchId = null` en el log).

### Mensaje

> **🎖️ ¡6 insignias nuevas en la vitrina!**
> 🧠 Dueño del Grupo, 🔮 Profeta de Penales, 🐺 Lobo Solitario, 🐑 Oveja Negra, 💔 Corazón Roto y 🙅 El Optimista.
> Algunas quizá ya son tuyas… entra a verlo.

- Link: `/pools/{poolId}/achievements` si el usuario pertenece a una sola sala, o `/pools` si pertenece a varias (mismo criterio que N2/N8/N11).
- "Algunas quizá ya son tuyas" es literal: al ser cálculo on-demand, el backfill es implícito — la primera visita ya muestra todo lo ganado hasta hoy.

### Implementación

El mecanismo de N11 quedó listo para reuso ("nueva constante + nueva fecha, cero cambios de esquema"). En `NotificationCheckService`:

```csharp
// N12: anuncio one-shot de la segunda tanda de insignias (10_insignias.md)
private const string BadgesAnnouncementType = "Announcement:insignias-v2";
private static readonly DateTime BadgesAnnouncementExpiresUtc = new(/* deploy + ~2 semanas */);
```

- `CheckAnnouncementsAsync` se generaliza para recorrer una lista de anuncios `(Type, ExpiresUtc, Title, Body)` en lugar del único N11 — o, más simple, se agrega un segundo bloque con la misma estructura (dedup por `NotificationLog.Type` por usuario, guardado por usuario para tolerar caídas a media corrida, expiración para no notificar a suscriptores tardíos meses después).
- Se dispara con el **primer ping** de la Azure Function tras el publish (~10 min máximo), igual que N11.
- **Requisito de orden:** N12 debe salir en el mismo deploy (o después) que LG1–LG3 — nunca anunciar insignias que aún no existen en el catálogo.

### Criterios de aceptación

- [x] Cada usuario suscrito recibe el anuncio exactamente una vez
- [x] El link abre la vitrina (sala única) o la lista de salas (varias)
- [x] Usuarios que se suscriben después de la fecha de expiración no lo reciben
- [x] Si la app se reinicia a media corrida, el siguiente ping envía solo a los que faltan
- [x] N11 sigue funcionando igual (su dedup usa otro `Type`)
