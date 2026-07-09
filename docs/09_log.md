# 09 — Log de visitas por sala

**Fecha:** 2026-07-09
**Estado:** ✅ Completado (2026-07-09)
**Contexto:** Módulos 0–8, A–L, I/J/K/K.1, M1–M4 y notificaciones N0–N4/N6–N10 completos (solo N5 pendiente de `07_notificaciones.md`). Este documento define un log de actividad por sala: cada vez que un usuario entra a una página del detalle de una sala se registra (usuario / página / URL / fecha), y el admin puede consultarlo en una vista paginada con filtro por usuario, accesible con un botón "Log" en el detalle de la sala.

---

## Decisiones de producto (confirmadas con el usuario)

| Tema | Decisión |
|------|----------|
| ¿Quién ve el botón "Log" y el módulo? | **Solo el admin** (dueño de la sala). Los demás miembros no ven el botón y la página les niega el acceso. Sus visitas **sí** se registran (incluidas las del propio admin). |
| Granularidad | **Toda visita genera una fila, sin dedup.** Cada acceso a una página = 1 registro. Con ~4 amigos y un torneo de 5 semanas el volumen es trivial; el log refleja la realidad tal cual. |
| Alcance | **Las 10 páginas de sala:** el detalle de la sala y sus 9 sub-páginas (ver catálogo abajo). La página de Log **no se loguea a sí misma** (es admin-only, solo metería ruido). |
| Qué se guarda | Usuario, **nombre de página** (amigable, no derivado de la URL), URL relativa y fecha/hora UTC. En la tabla se muestra el nombre de página; la URL queda guardada por si algún día se necesita (no se muestra en v1). |
| Tabla | Paginada (20 filas por página, más reciente primero) y filtrable por usuario (dropdown de miembros de la sala). Mobile first. |

### Catálogo de páginas logueadas

| Ruta | `PageName` |
|------|------------|
| `/pools/{id}` | Detalle de sala |
| `/pools/{id}/predictions` | Pronosticar |
| `/pools/{id}/my-predictions` | Mis pronósticos |
| `/pools/{id}/standings` | Tabla de posiciones |
| `/pools/{id}/standings/history` | Evolución |
| `/pools/{id}/standings/vs` | Comparar |
| `/pools/{id}/my-stats` | Mis estadísticas |
| `/pools/{id}/champion` | Campeón |
| `/pools/{id}/achievements` | Logros |
| `/pools/{id}/daily-summary` | Resumen diario |

Notas del alcance:

- **Una fila por entrada a la página**, no por interacción interna: cambiar de día en el Resumen diario (query string `date`), cambiar de jugadores en Comparar (`a`/`b`) o abrir un bottom sheet NO generan filas nuevas — el registro ocurre una sola vez, cuando el circuito interactivo de la página arranca.
- Solo se loguean visitas de **miembros de la sala** con sesión iniciada (las 10 páginas ya exigen `[Authorize]` + membresía; un no-miembro que fuerce la URL ve el mensaje de "sin acceso" y no se registra).

---

## Resumen de módulos

| # | Hecho | Módulo | Esfuerzo |
|---|:-----:|--------|----------|
| V1 | [x] | Entidad `PageVisitLog` + migración | ~30 min |
| V2 | [x] | `PageVisitService` (registrar + consultar paginado) | ~1 h |
| V3 | [x] | Componente `PageVisitLogger` + instrumentar las 10 páginas | ~1 h |
| V4 | [x] | Página `/pools/{poolId}/log` + botón en Pool Detail | ~2 h |

**Orden:** V1 → V2 → V3 y V4 (ambos dependen de V2; entre sí son independientes).

---

## Módulo V1 — Entidad `PageVisitLog` + migración

### Diseño

```csharp
// src/Quiniela.Data/Entities/PageVisitLog.cs
public class PageVisitLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int PoolId { get; set; }
    public Pool Pool { get; set; } = null!;
    public string PageName { get; set; } = null!;   // nvarchar(50)  — del catálogo
    public string Url { get; set; } = null!;        // nvarchar(300) — relativa, ej. "pools/1/standings"
    public DateTime VisitedAt { get; set; }         // UTC
}
```

Configuración en `QuinielaDbContext`:

- FKs `UserId` y `PoolId` en **`Restrict`** — mismo patrón que `Prediction`/`StandingsSnapshot`/`NotificationLog` para evitar multiple cascade paths en SQL Server (User → Pool → PageVisitLog tendría dos rutas).
- Índice **`(PoolId, VisitedAt)`** — cubre la consulta principal (log de una sala ordenado por fecha desc); el filtro por usuario es un `Where` adicional sobre pocas filas, no necesita índice propio.
- `PageName` max 50, `Url` max 300, ambos requeridos.

Migración: `AddPageVisitLog`. Si el bloqueo de "Application Control policy" del entorno reaparece al generarla, usar el workaround documentado (factory `IDesignTimeDbContextFactory` temporal en `Quiniela.Data`, ver notas del Módulo N0).

### Criterios de aceptación

- [x] Tabla `PageVisitLogs` creada con FKs en `Restrict` e índice `(PoolId, VisitedAt)`
- [x] Migración aplicada sin afectar tablas existentes

---

## Módulo V2 — `PageVisitService`

### Diseño

```csharp
// src/Quiniela.Web/Services/PageVisitService.cs
public class PageVisitService(IDbContextFactory<QuinielaDbContext> dbFactory)
{
    // Registra una visita. NUNCA debe tumbar la página que la llama:
    // try/catch total, silent-fail (mismo principio que PushNotificationService.SendAsync).
    public async Task LogAsync(int userId, int poolId, string pageName, string url);

    public record VisitRow(string DisplayName, string? ProfilePicturePath,
                           string PageName, DateTime VisitedAtUtc);
    public record VisitPage(List<VisitRow> Rows, int TotalCount);

    // Página de resultados: filtro opcional por usuario, orden VisitedAt desc,
    // Skip/Take en servidor (nunca se materializa la tabla completa).
    public async Task<VisitPage> GetPageAsync(int poolId, int? userId, int page, int pageSize);
}
```

Notas:

- Patrón `IDbContextFactory` + registro `Scoped` en `Program.cs`, como el resto de servicios.
- `GetPageAsync` hace dos consultas sobre el mismo `IQueryable` filtrado: `CountAsync()` para `TotalCount` y `OrderByDescending(v => v.VisitedAt).Skip((page-1)*pageSize).Take(pageSize)` con proyección a `VisitRow` (join a `User` para `DisplayName`/`ProfilePicturePath`).
- `LogAsync` trunca `url` a 300 chars por si acaso (query strings largos de Comparar) y guarda `DateTime.UtcNow`.

### Criterios de aceptación

- [x] `LogAsync` inserta la fila con fecha UTC y jamás propaga excepción al llamador
- [x] `GetPageAsync` regresa la página pedida ordenada por fecha desc y el total correcto
- [x] Con `userId` el resultado (filas y total) incluye solo a ese usuario

---

## Módulo V3 — Componente `PageVisitLogger` + instrumentación

### Objetivo

Un componente sin UI que cada página de sala declara con su nombre amigable — así el nombre queda definido en el punto de uso (no hay que traducir URLs después) e instrumentar una página nueva en el futuro es una línea.

### Diseño

```razor
@* src/Quiniela.Web/Components/Shared/PageVisitLogger.razor — no renderiza nada *@
@inject PageVisitService PageVisitService
@inject AuthenticationStateProvider AuthStateProvider
@inject NavigationManager NavigationManager

@code {
    [Parameter, EditorRequired] public int PoolId { get; set; }
    [Parameter, EditorRequired] public string PageName { get; set; } = null!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var idClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (idClaim is null) return;
        await PageVisitService.LogAsync(int.Parse(idClaim), PoolId, PageName,
            NavigationManager.ToBaseRelativePath(NavigationManager.Uri));
    }
}
```

Por qué `OnAfterRenderAsync(firstRender)` y no `OnInitializedAsync`:

- Las 10 páginas son `InteractiveServer` **con prerender**: `OnInitializedAsync` corre dos veces (prerender + circuito) y duplicaría cada visita. `OnAfterRenderAsync` corre solo en el circuito interactivo, exactamente una vez con `firstRender == true`.
- Navegaciones internas por query string (`NavigateTo(..., replace: true)` del Resumen diario, picks de Comparar) no re-disparan `firstRender` → una fila por entrada a la página, como se decidió.

Instrumentación — una línea al inicio del markup de cada una de las 10 páginas (el componente vive dentro de páginas que ya declaran su `@rendermode`, así que **no** necesita `@rendermode` propio en el punto de uso — a diferencia del precedente de `NotificationConsent` en `MainLayout`, que era hijo de un layout estático):

```razor
<PageVisitLogger PoolId="@PoolId" PageName="Tabla de posiciones" />
```

Detalle por página: en `Pools/Detail.razor` el parámetro de ruta se llama `Id` (no `PoolId`). Colocar el componente dentro de la rama `else` que ya valida `isMember` (así un no-miembro que fuerza la URL no se loguea); en las demás páginas, igual: después de su validación de membresía/carga, no antes.

### Criterios de aceptación

- [x] Entrar a cada una de las 10 páginas genera exactamente **una** fila (sin duplicado por prerender)
- [x] Cambiar de día en Resumen diario / de jugadores en Comparar no genera filas extra
- [x] Un usuario sin acceso a la sala no genera fila
- [x] Si `LogAsync` falla (BD caída), la página carga normal

---

## Módulo V4 — Página `/pools/{poolId}/log` + botón en Pool Detail

### Acceso

- Botón en `Pools/Detail.razor`, dentro del `d-flex gap-2 mb-4 flex-wrap` existente (después de "🎖️ Logros"), envuelto en `@if (isOwner)` (variable que la página ya calcula):

```razor
@if (isOwner)
{
    <a href="/pools/@Id/log" class="btn btn-outline-dark btn-sm">
        📜 Log
    </a>
}
```

- Página `Components/Pages/Pools/Log.razor(.css)` en `/pools/{poolId:int}/log`, `[Authorize]` + `InteractiveServer`. En `OnInitializedAsync` valida que el usuario actual sea el **owner** de la sala (`pool.OwnerId == currentUserId`, vía `PoolService.GetPoolWithMembersAsync` — la misma llamada surte el dropdown de miembros); si no, el mismo mensaje "Sala no encontrada o no tienes acceso" que usa el detalle.
- Breadcrumb: `Mis Salas → {sala} → Log`.
- Esta página **no** incluye `PageVisitLogger` (decisión de producto: no se loguea a sí misma).

### UI (mobile first)

Clases nuevas `log-*` con las CSS vars `--q-*` de `theme.css`, breakpoint **641px** `min-width` (el estándar del proyecto):

- **Filtro:** un `<select>` (`log-filter`) con "Todos los miembros" + un item por miembro (`DisplayName`, ordenados alfabéticamente como en Detail). Cambiarlo recarga la **página 1** con el filtro aplicado.
- **Mobile (base):** lista de filas compactas tipo tarjeta (patrón ya usado en Resumen diario/Comparar): avatar circular 28px (o el placeholder ⚽ de siempre), `DisplayName` en negrita, nombre de página, y la fecha/hora abajo en `--q-text-muted`.
- **Desktop (≥641px):** tabla de 3 columnas — Usuario (avatar + nombre) / Página / Fecha — misma data, layout `table` en vez de tarjetas (solo CSS, sin duplicar markup: filas `display:flex` en mobile → `display:table-row` en desktop, o dos bloques `@media` sobre las mismas clases).
- **Paginación:** barra inferior `◀ Anterior · Página X de Y · Siguiente ▶` (botones deshabilitados en los extremos). `pageSize = 20`. Sin números de página sueltos — en mobile no caben y con este volumen no hacen falta.
- **Vacío:** "Aún no hay visitas registradas" (o "…de este usuario" con filtro activo).

### Fechas

`VisitedAt` se guarda en UTC y se muestra en la **zona horaria del navegador** con el patrón ya existente de `Predictions/Index`: `quiniela.getClientTimezone()` vía JS interop en `OnAfterRenderAsync` + `TimeZoneInfo.ConvertTimeFromUtc` (fallback CDMX si el interop falla). Formato corto: `08 jul · 21:35`.

### Comportamiento del estado

- `page` y `userId` del filtro viven en el estado del componente (sin query string en v1 — no hay caso de uso de compartir un link a una página específica del log).
- Cada cambio de página/filtro llama `GetPageAsync` de nuevo (server-side paging real, no se trae todo a memoria).

### Criterios de aceptación

- [x] El botón "📜 Log" solo lo ve el owner de la sala, y navega a `/pools/{poolId}/log`
- [x] Un miembro no-owner que fuerza la URL ve el mensaje de sin acceso
- [x] La tabla muestra usuario (avatar + nombre), página y fecha local, más reciente primero
- [x] Filtrar por usuario reinicia a página 1 y el total/páginas se recalculan
- [x] La paginación navega correctamente y deshabilita ◀/▶ en los extremos
- [x] En 390px se lee cómodo (tarjetas); en ≥641px se ve como tabla
- [x] Estado vacío con mensaje amigable
