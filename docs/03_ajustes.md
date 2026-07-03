# Plan de Ajustes — Quiniela Mundial 2026
## Archivo: 03_ajustes.md

---

## ✅ Ajuste 1 — Indicador "En Curso" en Mis Pronósticos (tab Pendientes)

### Objetivo
Cuando un partido en el tab "Pendientes" ya empezó pero todavía no tiene resultado capturado, resaltar visualmente la tarjeta con un header de color diferente y un badge "En Curso" con punto rojo parpadeante.

### Definición de "en curso"
Un partido se considera **en curso** cuando:
```
Match.KickoffUtc <= DateTime.UtcNow  AND  Match.Status == Programado
```
No existe un estado intermedio en la BD — se infiere por tiempo. El partido permanece "en curso" hasta que el admin capture el resultado (cambia a `Finalizado`).

### Cambios de diseño

**Header de la MatchCard:**
- Estado normal: gradiente `#0D1B2A → #1A2B42` (azul oscuro actual)
- Estado **en curso**: gradiente rojo-oscuro `#7F1D1D → #991B1B`
- Badge nuevo: `● EN CURSO` — punto SVG animado en rojo + texto en blanco, posicionado en la esquina superior derecha del header

**CSS — keyframe del punto parpadeante:**
```css
@keyframes livePulse {
    0%, 100% { opacity: 1; transform: scale(1); }
    50%       { opacity: 0.4; transform: scale(0.85); }
}

.live-dot {
    display: inline-block;
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background: #EF4444;
    animation: livePulse 1.2s ease-in-out infinite;
    margin-right: 5px;
    vertical-align: middle;
}

.mc-header-live {
    background: linear-gradient(135deg, #7F1D1D 0%, #991B1B 100%) !important;
}

.mc-live-badge {
    display: inline-flex;
    align-items: center;
    background: rgba(239,68,68,0.2);
    border: 1px solid rgba(239,68,68,0.5);
    border-radius: 4px;
    padding: 2px 6px;
    font-size: 0.56rem;
    font-weight: 700;
    color: #FCA5A5;
    letter-spacing: 0.08em;
    margin-top: 3px;
}
```

### Archivos a modificar

| Archivo | Cambio |
|---|---|
| `Components/Shared/MatchCard.razor` | Agregar propiedad `IsInProgress` + clases CSS condicionales |
| `Components/Shared/MatchCard.razor.css` | Agregar `.mc-header-live`, `.mc-live-badge`, `.live-dot` |
| `wwwroot/css/animations.css` | Agregar `@keyframes livePulse` |

### Implementación — MatchCard.razor

```csharp
// Propiedad nueva en @code
private bool IsInProgress => IsLocked && Match.Status == MatchStatus.Programado;
```

```razor
@* Header — condición IsInProgress *@
<div class="mc-header @(IsInProgress ? "mc-header-live" : "")">
    <div class="mc-header-info">
        <span class="mc-stage">⚽ GRUPO @Match.GroupCode</span>
        @if (!string.IsNullOrWhiteSpace(Match.Venue))
        {
            <span class="mc-venue">@Match.Venue.ToUpperInvariant()</span>
        }
        @if (IsInProgress)
        {
            <span class="mc-live-badge">
                <span class="live-dot"></span> EN CURSO
            </span>
        }
    </div>
    ...
</div>
```

### Criterio de aceptación
- Partidos cuyo `KickoffUtc` ya pasó y siguen en `Programado` muestran header rojo con badge "● EN CURSO".
- Partidos `Finalizados` y partidos futuros no muestran el badge.
- El punto rojo parpadea suavemente (no hace flash brusco).
- En el tab "Finalizados" no aplica (esos ya tienen resultado).

### Estimación: 1–2 horas

---

## ✅ Ajuste 2 — Ventana flotante al tocar bandera de equipo (tab Finalizados)

### Objetivo
Al tocar la bandera (flag) de un equipo en una tarjeta del tab "Finalizados", abrir un **bottom sheet** con información del equipo: partidos anteriores, próximos partidos y posición en el grupo.

### Diseño — Bottom Sheet (mobile-first)

```
┌─────────────────────────────────────┐
│  [─────] ← drag handle              │ ← 75% altura pantalla
│  🇲🇽  MÉXICO  •  GRUPO A            │
│  ─────────────────────────────────  │
│  POSICIÓN EN GRUPO                  │
│  1° │ 2G 0E 0P │ GF 4 GC 1 │ 6 pts │
│                                     │
│  PARTIDOS ANTERIORES                │
│  MEX 2–0 POL  (Jun 11)              │
│  ✅ Tu pronóstico: MEX  · +3 pts    │
│  MEX 2–1 ARG  (Jun 15)              │
│  ❌ Tu pronóstico: Empate · 0 pts   │
│                                     │
│  PRÓXIMOS PARTIDOS                  │
│  ⏳ MEX vs USA   (Jun 19 · 19:00)   │
└─────────────────────────────────────┘
```

**Scope:** partidos de todo el torneo para ese equipo (global, no depende de la sala).

### Indicador de interactividad en la tarjeta

Para que el usuario sepa que puede tocar la bandera, se agrega en el tab Finalizados una leyenda discreta debajo del acordeón activo (una sola vez, no por tarjeta):

```
💡 Toca la bandera de un equipo para ver su historial
```

- Texto pequeño (`0.72rem`), color `text-muted`, ícono de punta de dedo o información.
- Se muestra solo en el tab Finalizados, debajo del primer acordeón abierto.
- Desaparece una vez que el usuario haya tocado una bandera por primera vez en la sesión (usando una variable booleana `_hintSeen` en el componente).

Adicionalmente, en la propia tarjeta, las banderas en modo ReadOnly (Finalizados) cambian su cursor a `pointer` y tienen un borde sutil pulsante para indicar interactividad:

```css
.mc-flag-tappable {
    cursor: pointer;
    transition: transform 0.15s ease, box-shadow 0.15s ease;
}
.mc-flag-tappable:hover,
.mc-flag-tappable:active {
    transform: scale(1.1);
    box-shadow: 0 0 0 3px rgba(26,86,219,0.3);
}
```

### Datos mostrados
1. **Cabecera**: bandera grande + nombre completo + grupo
2. **Posición en grupo**: fila compacta con Pos, G/E/P, GF/GC, Pts
3. **Partidos anteriores** (`Status == Finalizado`): resultado real, indicador de victoria/empate/derrota, fecha, **y resultado del pronóstico del usuario** (acierto/fallo/sin pronóstico)
4. **Próximos partidos** (`Status == Programado`): fecha, hora local, rival

### Nuevos archivos

| Archivo | Tipo | Propósito |
|---|---|---|
| `Components/Shared/TeamSheet.razor` | Componente | Bottom sheet con info del equipo |
| `Components/Shared/TeamSheet.razor.css` | CSS scoped | Estilos del bottom sheet |
| `Services/TeamSheetService.cs` | Servicio | Queries de partidos y posición del equipo |

### TeamSheetService — métodos

```csharp
public class TeamSheetMatchEntry
{
    public Match Match { get; set; } = null!;
    public Prediction? UserPrediction { get; set; }  // null = sin pronóstico
}

public class TeamSheetData
{
    public Team Team { get; set; } = null!;
    public GroupStanding? GroupPosition { get; set; }       // posición en grupo
    public List<TeamSheetMatchEntry> PreviousMatches { get; set; } = [];
    public List<Match> UpcomingMatches { get; set; } = [];
}

// Método principal — necesita userId y poolId para recuperar las predicciones del usuario
public Task<TeamSheetData> GetTeamSheetAsync(int teamId, int userId, int poolId);
```

La query obtiene todos los partidos donde `HomeTeamId == teamId OR AwayTeamId == teamId`, los separa en anteriores/próximos, calcula la posición del grupo y hace join con `Predictions` del usuario para mostrar su pronóstico (si existe) en cada partido anterior.

**Indicador de pronóstico en partidos anteriores:**
- ✅ `+X pts` — acertó el 1X2 (verde)
- ❌ `0 pts` — falló el 1X2 (gris/rojo suave)
- `—` — no hizo pronóstico para ese partido (neutro)

### Modificaciones al flujo en MyPredictions

En `MyPredictions.razor`, pasar un callback `OnFlagTapped` al `MatchCard`. Cuando el usuario toca una bandera en modo `ReadOnly` (tab Finalizados), se activa el sheet.

**Alternativa más simple:** Manejar el tap directamente dentro de `MatchCard.razor` para no propagar eventos hacia arriba — `MatchCard` ya tiene `IJSRuntime` inyectado. Mostrar el `TeamSheet` como componente hijo condicional dentro del mismo MatchCard o como overlay global.

**Recomendación:** Usar un componente `TeamSheet` global en `MyPredictions.razor` con una variable `selectedTeamId` que se actualiza al tocar la bandera. Evita instanciar múltiples sheets.

### CSS del Bottom Sheet

```css
.team-sheet-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0,0,0,0.5);
    z-index: 1000;
    animation: fadeIn 0.2s ease;
}

.team-sheet-panel {
    position: fixed;
    bottom: 0;
    left: 0;
    right: 0;
    max-height: 78dvh;
    background: #fff;
    border-radius: 20px 20px 0 0;
    overflow-y: auto;
    animation: slideUpSheet 0.3s ease;
    z-index: 1001;
    padding: 16px 20px 32px;
}

@keyframes slideUpSheet {
    from { transform: translateY(100%); }
    to   { transform: translateY(0); }
}
```

### Criterio de aceptación
- Tocar la bandera de cualquier equipo en el tab Finalizados abre el bottom sheet.
- El sheet muestra posición del grupo, partidos anteriores con resultado y próximos con fecha/hora.
- Tocar fuera del sheet o el botón "×" lo cierra.
- Si el equipo no tiene partidos anteriores o próximos, se muestra "Sin partidos" en esa sección.
- El sheet es scrollable si el contenido supera el alto de pantalla.

### Estimación: 4–6 horas

---

## ✅ Ajuste 3 — Menú "Grupos" con tabla de posiciones por grupo

### Objetivo
Nueva sección global en el menú principal que muestre la tabla de posiciones de cada grupo (A–L) en tiempo real, calculada directamente desde la tabla `Matches`. No requiere nueva tabla en BD.

### Enfoque técnico: cálculo en tiempo real desde `Matches`

Los standings se calculan en cada petición directamente desde la tabla `Matches` (sin tabla adicional en BD). Para 72 partidos de grupos, la query es trivial. Si en el futuro hay problemas de rendimiento se puede agregar `IMemoryCache` que se invalide al capturar un resultado.

### Estructura de la posición por grupo

```csharp
public class GroupStanding
{
    public Team Team { get; set; } = null!;
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDiff  => GoalsFor - GoalsAgainst;
    public int Points    => Won * 3 + Drawn;
}
```

**Criterios de desempate (FIFA oficial):**
1. Puntos
2. Diferencia de goles (global)
3. Goles a favor (global)
4. Diferencia de goles en enfrentamiento directo
5. Goles a favor en enfrentamiento directo

### Diseño de la página

**Ruta:** `/groups` o `/grupos`
**Acceso:** menú principal (todos los usuarios autenticados)
**Navegación entre grupos:** acordeón (12 grupos A–L, todos visibles, cada uno expandible)

Cada acordeón contiene:

```
GRUPO A                                    ▼
┌─────────────────────────────────────────┐
│  #  Equipo         PJ  G  E  P  DG  Pts │
│  1  🇲🇽 México      2  2  0  0  +3    6  │
│  2  🇵🇱 Polonia     2  1  0  1  +1    3  │
│  3  🇸🇦 Arabia S.   2  0  1  1  -2    1  │
│  4  🇦🇷 Argentina   2  0  1  1  -2    1  │
└─────────────────────────────────────────┘
Partidos jugados: 3 / 6
```

- Equipos que clasifican (1° y 2°): fondo verde muy sutil
- Tercer lugar con posibilidades: fondo amarillo muy sutil
- Equipos eliminados: sin fondo especial

### Nuevos archivos

| Archivo | Tipo | Propósito |
|---|---|---|
| `Services/GroupStandingsService.cs` | Servicio | Calcula standings de todos los grupos |
| `Components/Pages/Groups/Index.razor` | Página | Vista principal de grupos |
| `Components/Pages/Groups/Index.razor.css` | CSS scoped | Estilos de la tabla de grupos |

### GroupStandingsService — método principal

```csharp
public class GroupStandingsService
{
    // Devuelve diccionario: GroupCode ('A'..'L') → lista de 4 equipos ordenados
    public Task<Dictionary<char, List<GroupStanding>>> GetAllGroupStandingsAsync();

    // Cálculo interno por grupo:
    // 1. Obtener todos los Matches con Status == Finalizado y GroupCode == group
    // 2. Para cada Team en el grupo, acumular G/E/P/GF/GC desde los partidos
    // 3. Ordenar por: Pts DESC → DG DESC → GF DESC
}
```

### Modificaciones al NavMenu

```razor
@* NavMenu.razor — nuevo ítem *@
<div class="nav-item px-3">
    <NavLink class="nav-link" href="grupos">
        <span class="bi bi-table-nav-menu" aria-hidden="true"></span> Grupos
    </NavLink>
</div>
```

Agregar icono `bi-table-nav-menu` en `NavMenu.razor.css` (SVG de tabla/grid).

### Criterio de aceptación
- La página `/grupos` es accesible desde el menú para todos los usuarios autenticados.
- Se muestran los 12 grupos en acordeón; por defecto todos colapsados (o el A abierto).
- Cada grupo muestra la tabla con PJ, G, E, P, DG, Pts ordenada correctamente.
- Cuando no hay partidos jugados en un grupo, la tabla muestra los 4 equipos con todos en 0.
- El primer y segundo lugar tienen un fondo verde sutil para indicar clasificación.
- Si no todos los partidos del grupo están jugados, se muestra "X / 6 partidos jugados" debajo de la tabla.
- La tabla se actualiza en tiempo real al hacer refresh (InteractiveServer).

### Estimación: 4–6 horas

---

## Resumen de estimaciones

| Ajuste | Esfuerzo | Prioridad sugerida |
|---|---|---|
| 1 — Indicador "En Curso" | 1–2 h | Alta (impacto inmediato durante el torneo) |
| 2 — Bottom sheet de equipo | 4–6 h | Media |
| 3 — Menú Grupos | 4–6 h | Alta (útil durante todo el torneo) |
| **Total** | **9–14 h** | |

## Orden de implementación sugerido

1. **Ajuste 3 (Grupos)** — base de datos de grupos necesaria para el Ajuste 2 (`GroupStanding`)
2. **Ajuste 1 (En Curso)** — cambio visual sencillo, alto impacto durante partidos
3. **Ajuste 2 (Bottom Sheet)** — reutiliza lógica de grupos del Ajuste 3
