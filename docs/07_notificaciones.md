# 07 — Sistema de Notificaciones Push

**Fecha:** 2026-07-06  
**Estado:** Pendiente  
**Contexto:** Módulos 0–8, A–H (`06_mejorasfases.md`) y I/J/K/K.1 (`mejoras_1.md`) completos. El torneo está en fase activa con usuarios reales. Este documento define un sistema de notificaciones push (Web Push / VAPID) para mantener a los jugadores enganchados sin que tengan que abrir la app proactivamente.

---

## Infraestructura Azure

El App Service actual corre en el tier **F1 (Free)**. Este tier no soporta "Always On", por lo que la app se duerme tras 20 minutos de inactividad y cualquier `BackgroundService` muere con ella.

### Solución: F1 + Azure Functions Consumption (gratis)

Las notificaciones se dividen en dos categorías según cómo se disparan:

| Categoría | Disparador | Funciona en F1 sin cambios |
|-----------|-----------|---------------------------|
| **Por acción** (N1, N3, N4, N6, N7) | Admin guarda resultado / jugador pronostica → app ya está despierta | ✅ Sí |
| **Por tiempo** (N2, N5, N8, N9) | Necesitan revisar cada 10 min aunque nadie esté en la app | ❌ No |

Para las notificaciones por tiempo se agrega un **Azure Function con timer** (Consumption plan, gratis hasta 1M ejecuciones/mes) que pingea la Blazor app cada 10 minutos. El ping despierta la app y dispara la revisión — toda la lógica queda en la Blazor app, la Function solo actúa como reloj externo.

```
cada 10 minutos
      │
Azure Function (Consumption — gratis)
      │  POST /api/notify/check
      │  X-Notify-Secret: <guid>
      ▼
App Service F1 (despierta si dormida, ~5 seg cold start)
      │
      ├── ¿Partidos sin pronosticar en <60 min? → push a usuarios   [N2]
      ├── ¿Ventana campeón abierta/cerrando?    → push a usuarios   [N5]
      ├── ¿Partido comenzó en últimos 10 min?   → push a usuarios   [N8]
      └── ¿Ya son las 22:00 CDMX?               → resumen diario    [N9]

Eventos por acción del admin/jugador
      │
      ├── Admin guarda resultado → push inmediato                    [N1]
      ├── Resultado cambia posición → push inmediato                 [N3]
      ├── Resultado desbloquea insignia → push inmediato             [N4]
      ├── Admin asigna cruce KO → push inmediato                     [N6]
      └── Último jugador pronostica → push inmediato                 [N7]
```

### Nuevo proyecto: `src/Quiniela.Notifier/`

Azure Function en proyecto separado dentro del mismo repositorio:

```
src/
├── Quiniela.Data/
├── Quiniela.Web/
└── Quiniela.Notifier/          ← nuevo
    ├── Quiniela.Notifier.csproj
    └── TimerFunction.cs
```

```csharp
// src/Quiniela.Notifier/TimerFunction.cs
public class TimerFunction(IHttpClientFactory http, IConfiguration config)
{
    [Function("NotifyCheck")]
    public async Task Run([TimerTrigger("0 */10 * * * *")] TimerInfo timer)
    {
        var url    = config["AppService:NotifyUrl"];
        var secret = config["AppService:NotifySecret"];

        var client = http.CreateClient();
        client.DefaultRequestHeaders.Add("X-Notify-Secret", secret);
        await client.PostAsync(url, null);
    }
}
```

### Endpoint en la Blazor app

```csharp
// Program.cs — agregar junto a los otros app.Map...
app.MapPost("/api/notify/check", async (
    HttpContext ctx,
    IConfiguration config,
    NotificationCheckService notifSvc) =>
{
    var secret = ctx.Request.Headers["X-Notify-Secret"].ToString();
    if (secret != config["Push:NotifySecret"])
        return Results.Unauthorized();

    await notifSvc.CheckAndNotifyAsync();
    return Results.Ok();
});
```

`NotificationCheckService` es un servicio normal (no `BackgroundService`) con un único método público `CheckAndNotifyAsync` que contiene la lógica de revisión de partidos próximos y ventana de campeón. Se registra como `Scoped` en `Program.cs`.

### Variables de configuración

**App Service** → Configuration → Application settings:

```
Push__VapidPublicKey     = <clave pública generada con WebPushClient.GenerateVapidKeys()>
Push__VapidPrivateKey    = <clave privada>
Push__Subject            = mailto:haterz133@gmail.com
Push__NotifySecret       = <guid aleatorio, ej: 8f3a2b1c-...>
```

**Function App** → Configuration → Application settings:

```
AppService__NotifyUrl    = https://<tu-app>.azurewebsites.net/api/notify/check
AppService__NotifySecret = <el mismo guid de arriba>
```

### Costo

| Recurso | Tier | $/mes |
|---------|------|-------|
| App Service | F1 (sin cambios) | $0 |
| Azure Function App | Consumption | $0 |
| Storage Account (requerida por Functions) | LRS mínimo | ~$0.02 |
| **Total extra** | | **~$0** |

---

## Consideración de diseño: multi-sala

Un usuario puede pertenecer a varias salas. Los eventos se dividen en dos categorías:

| Categoría | Granularidad | Incluye nombre de sala |
|-----------|-------------|------------------------|
| Eventos de **partido** (resultado, cierre) | Una notificación por usuario (todas las salas) | No / opcional |
| Eventos de **sala** (posición, insignia, campeón) | Una notificación por sala afectada | **Sí siempre** |

En la práctica casi todos los usuarios pertenecen a una sola sala, por lo que el ruido de multi-sala es bajo.

---

## Resumen de prioridades

| # | Hecho | Módulo | Urgencia | Esfuerzo | Impacto |
|---|:-----:|--------|----------|----------|---------|
| N0 | [x] | Infraestructura base Web Push (VAPID) | 🔴 Alta | ~4–5 h | Bloqueante |
| N1 | [x] | Resultado capturado | 🔴 Alta | ~2–3 h | Muy alto |
| N2 | [x] | Pronóstico cerrando pronto | 🔴 Alta | ~3–4 h | Alto |
| N3 | [x] | Cambio de posición en la tabla | 🟡 Media | ~2–3 h | Alto |
| N4 | [x] | Insignia desbloqueada | 🟡 Media | ~2–3 h | Medio |
| N5 | [ ] | Ventana de campeón abierta / cerrando | 🟡 Media | ~2 h | Medio |
| N6 | [x] | Nuevo cruce KO disponible | 🟢 Baja | ~1–2 h | Medio |
| N7 | [x] | Todos los jugadores ya pronosticaron | 🟢 Baja | ~1–2 h | Bajo |
| N8 | [x] | Partido comenzado | 🟢 Baja | ~1–2 h | Medio |
| N9 | [x] | Resumen diario a las 22:00 (link al Módulo L) | 🟡 Media | ~2 h | Alto |
| N10 | [x] | Mejor/peor del día a las 21:30 (ver `08_insignias_mejorpeor.md`) | 🟡 Media | ~2 h | Alto |
| N11 | [x] | Anuncio one-shot: nuevos logros mejor/peor del día | 🟢 Baja | ~1–2 h | Medio |

**Orden de implementación sugerido:** N0 → N1 → N2 → N3 → N4 → N5 → N6 → N7 → N8 → N9  
N0 es bloqueante: sin infraestructura base no se puede implementar ninguna otra.  
N10 se documenta en `08_insignias_mejorpeor.md` (nació con las insignias mejor/peor). N11 depende de que N10 esté en producción — anuncia esos logros.

---

## Módulo N0 — Infraestructura base Web Push (VAPID)

### Objetivo

Establecer la base técnica que todos los módulos siguientes comparten: claves VAPID, almacenamiento de suscripciones por usuario, service worker actualizado para recibir eventos push, y UI mínima para pedir permiso.

### Contexto técnico

El proyecto ya tiene `src/Quiniela.Web/wwwroot/service-worker.js` (actualmente vacío funcional). Web Push no requiere infraestructura externa — el servidor envía el push directamente al endpoint del browser vía HTTPS usando las claves VAPID. No hay costo por volumen en esta escala (docenas de usuarios).

### Paquete NuGet

```
dotnet add src/Quiniela.Web package WebPush
```

### Nueva entidad

```csharp
// src/Quiniela.Data/Entities/PushSubscription.cs
public class PushSubscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";   // clave pública del browser
    public string Auth { get; set; } = "";     // secreto del browser
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

Migración: `AddPushSubscription`  
FK `UserId → Restrict` (mismo patrón que `Prediction`/`PoolMember`).

### Nuevo servicio

```csharp
// src/Quiniela.Web/Services/PushNotificationService.cs
public class PushNotificationService(IDbContextFactory<QuinielaDbContext> dbFactory,
                                      IConfiguration config)
{
    // Envía push a todos los dispositivos suscritos de un usuario
    public async Task SendAsync(int userId, string title, string body, string? url = null);

    // Guarda o actualiza la suscripción del dispositivo actual
    public async Task UpsertSubscriptionAsync(int userId, string endpoint,
                                               string p256dh, string auth);

    // Elimina la suscripción (cuando el usuario revoca permiso)
    public async Task RemoveSubscriptionAsync(string endpoint);
}
```

Las claves VAPID se generan una sola vez con `WebPushClient.GenerateVapidKeys()` y se guardan en User Secrets (`Push:VapidPublicKey` / `Push:VapidPrivateKey`), nunca en el repo.

### Service worker

```javascript
// wwwroot/service-worker.js — agregar handler de push
self.addEventListener('push', event => {
    const data = event.data?.json() ?? {};
    event.waitUntil(
        self.registration.showNotification(data.title ?? 'Quiniela', {
            body: data.body,
            icon: '/icons/icon-192.png',
            data: { url: data.url ?? '/' }
        })
    );
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    event.waitUntil(clients.openWindow(event.notification.data.url));
});
```

### UI — pedir permiso

Pequeño componente `NotificationConsent.razor` que aparece **una sola vez** en `MainLayout.razor` (dentro del `AuthorizeView`) si el usuario está logueado y no tiene suscripción registrada. Usa `IJSRuntime` para llamar `Notification.requestPermission()` y luego `PushManager.subscribe()`. El resultado se envía a `PushNotificationService.UpsertSubscriptionAsync`.

### Criterios de aceptación

- [x] Claves VAPID generadas y en User Secrets
- [x] Migración `AddPushSubscription` aplicada
- [x] Service worker recibe eventos push y muestra notificación nativa
- [x] Click en notificación abre la URL correcta
- [x] El consentimiento se pide una sola vez (no en cada carga)
- [x] Un usuario puede tener múltiples dispositivos suscritos (un endpoint por dispositivo)

---

## Módulo N1 — Resultado capturado

### Objetivo

Notificar a cada jugador cuando el admin finaliza un partido, indicando si acertó o falló el pronóstico.

### Mensaje

> ⚽ **México 2–1 Argentina**  
> ✅ Acertaste — +3 pts  *(o: ❌ Fallaste esta vez)*

El link lleva a `/pools/{id}/predictions` de la primera sala del usuario donde ese partido aplica.

### Implementación

Hook al final de `ScoringService.RecalculateForMatchAsync` (ya existe en `src/Quiniela.Web/Services/ScoringService.cs`):

```csharp
// Después de SaveChangesAsync — notificar a cada jugador afectado
foreach (var prediction in predictions)
{
    var acertó = prediction.Points > 0;
    var body = acertó
        ? $"✅ Acertaste — +{prediction.Points} pts"
        : "❌ Fallaste esta vez";
    await pushSvc.SendAsync(prediction.UserId, $"⚽ {matchLabel}", body, $"/pools/{poolId}/predictions");
}
```

`matchLabel` se arma con `HomeTeam.ShortCode`/`AwayTeam.ShortCode` igual que en el resto del proyecto.  
Un usuario en 3 salas recibe **una sola notificación** (los puntos son iguales en todas — el partido es el mismo).

### Criterios de aceptación

- [x] Notificación llega al celular dentro de los 5 segundos de que el admin guarda el resultado
- [x] Muestra correctamente si acertó o falló
- [x] El link lleva a la página de predicciones de su sala
- [x] Si el usuario no tiene suscripción activa, no lanza excepción (silent fail)

---

## Módulo N2 — Pronóstico cerrando pronto

### Objetivo

Recordar a los jugadores que tienen partidos sin pronosticar cuando faltan ~60 minutos para el kickoff.

### Mensaje (agrupado si son varios partidos)

> ⏰ **Faltan 60 min — México vs. Argentina**  
> Aún no has pronosticado este partido

Si hay varios partidos próximos sin pronóstico:
> ⏰ **3 partidos cierran pronto**  
> Entra antes de que arranquen

### Implementación

La lógica vive en `NotificationCheckService` (`src/Quiniela.Web/Services/NotificationCheckService.cs`), invocada desde el endpoint `/api/notify/check` que pingea la Azure Function cada 10 minutos (ver sección Infraestructura Azure):

```csharp
// src/Quiniela.Web/Services/NotificationCheckService.cs
public class NotificationCheckService(IDbContextFactory<QuinielaDbContext> dbFactory,
                                       PushNotificationService pushSvc)
{
    public async Task CheckAndNotifyAsync()
    {
        await CheckUpcomingMatchesAsync();   // N2
        await CheckChampionWindowAsync();    // N5
        await CheckStartedMatchesAsync();    // N8
    }

    private async Task CheckUpcomingMatchesAsync()
    {
        var now = DateTime.UtcNow;
        var window = (from: now.AddMinutes(50), to: now.AddMinutes(70));
        // buscar partidos en ventana sin pronóstico por usuario...
    }
}
```

Busca partidos que:
1. `KickoffUtc` entre `[now + 50min, now + 70min]` (ventana de 20 min para no enviar duplicados entre pings)
2. Tienen al menos un jugador en alguna sala que **no** hizo pronóstico

Para evitar duplicados se guarda en la BD el conjunto de `(userId, matchId)` ya notificados — en memoria no es confiable porque la app puede reiniciarse entre pings en F1.

Registrado en `Program.cs` como `Scoped`:
```csharp
builder.Services.AddScoped<NotificationCheckService>();
```

### Criterios de aceptación

- [x] Notificación llega ~60 min antes del kickoff
- [x] No se envía si el jugador ya pronosticó
- [x] No se envía dos veces al mismo jugador por el mismo partido
- [x] Usuarios sin suscripción activa se omiten silenciosamente

---

## Módulo N3 — Cambio de posición en la tabla

### Objetivo

Notificar a cada jugador cuando sube o baja de posición tras finalizar un partido, **por sala**.

### Mensaje

> 📊 **¡Subiste al 2° lugar!**  
> Sala: Quiniela Amigos 2026

> 📊 **Bajaste al 3° lugar**  
> Sala: Quiniela Oficina

Sin cambio de posición: no se envía notificación.

### Implementación

Hook en `ScoringService.SaveSnapshotAsync` (ya guarda `StandingsSnapshot`). Comparar la posición del snapshot nuevo vs. el inmediatamente anterior para cada `(userId, poolId)`:

```csharp
var previous = await db.StandingsSnapshots
    .Where(s => s.PoolId == poolId && s.UserId == userId && s.MatchId != currentMatchId)
    .OrderByDescending(s => s.Match.KickoffUtc)
    .FirstOrDefaultAsync();

if (previous != null && previous.Position != currentPosition)
{
    var direction = currentPosition < previous.Position ? "⬆️ Subiste" : "⬇️ Bajaste";
    await pushSvc.SendAsync(userId,
        $"{direction} al {currentPosition}° lugar",
        $"Sala: {pool.Name}",
        $"/pools/{poolId}/standings");
}
```

### Criterios de aceptación

- [x] Notificación llega tras cada finalización de partido que cambia posiciones
- [x] Muestra la posición nueva (no la delta)
- [x] Incluye el nombre de la sala
- [x] No se envía si la posición no cambió
- [x] Un usuario en 2 salas puede recibir 2 notificaciones distintas (una por sala)

---

## Módulo N4 — Insignia desbloqueada

### Objetivo

Notificar al jugador cuando obtiene una nueva insignia en una sala, en tiempo real (no solo en la próxima visita).

Actualmente la animación de "desbloqueo" se detecta del lado del cliente comparando con `localStorage`. Este módulo complementa eso con una notificación push cuando el usuario **no está en la app**.

### Mensaje

> 🔥 **¡Nueva insignia desbloqueada!**  
> "Racha de Fuego" en Quiniela Amigos 2026

### Implementación

`AchievementsService.GetForPoolAsync` ya calcula el set de insignias de cada jugador. Para detectar "insignia nueva" se necesita persistir qué insignias ya se notificaron — se agrega una tabla simple o se reutiliza una columna JSON:

**Opción A (simple):** nueva tabla `NotifiedAchievements (UserId, PoolId, AchievementKey)` — registra qué claves ya se notificaron. Al comparar el set nuevo vs. el registrado, cualquier diferencia es una insignia nueva.

**Opción B (sin migración):** columna `string? NotifiedAchievementKeys` en `PoolMember` (JSON serializado). Más simple pero mezcla responsabilidades.

**Se recomienda Opción A.**

Hook en `ScoringService.RecalculateForMatchAsync` (ya llama a `SaveSnapshotAsync`):

```csharp
foreach (var member in poolMembers)
{
    var achievements = await achievementsSvc.GetForPoolAsync(poolId, member.UserId);
    var newOnes = achievements.Except(previouslyNotified[member.UserId]);
    foreach (var ach in newOnes)
    {
        await pushSvc.SendAsync(member.UserId,
            $"{ach.Icon} ¡Insignia desbloqueada!",
            $"\"{ach.Name}\" en {pool.Name}",
            $"/pools/{poolId}/achievements");
    }
}
```

### Criterios de aceptación

- [x] Notificación llega la primera vez que el jugador gana cada insignia
- [x] No se repite si ya fue notificada
- [x] Muestra el nombre y sala correctos
- [x] Migración `AddNotifiedAchievements` aplicada

---

## Módulo N5 — Ventana de campeón abierta / cerrando

### Objetivo

Dos notificaciones específicas al ciclo de vida del pronóstico de campeón (`ChampionService`):

1. **Ventana abierta:** cuando todos los Dieciseisavos están finalizados — los jugadores ya pueden pronosticar al campeón.
2. **Ventana cerrando:** ~60 min antes del kickoff del primer partido de Octavos — última oportunidad.

### Mensajes

> 👑 **¡Ya puedes pronosticar al Campeón!**  
> Todos los Dieciseisavos terminaron. Elige tu candidato antes de que arranquen los Octavos.  
> Sala: Quiniela Amigos 2026

> 👑 **Última hora — pronóstico de campeón**  
> Cierra en ~60 min cuando arranquen los Octavos.  
> Sala: Quiniela Amigos 2026

### Implementación

`ChampionService.GetWindowStateAsync` ya calcula el estado (`NotYetOpen / Open / Closed`). La lógica de revisión vive en `NotificationCheckService.CheckChampionWindowAsync` (el mismo servicio de N2), invocada desde el mismo endpoint `/api/notify/check` que pingea la Azure Function cada 10 minutos:

- Si el estado **cambia de `NotYetOpen` → `Open`**: enviar notificación tipo 1 a todos los miembros sin pronóstico de campeón.
- Si el primer Octavos kickoff está en `[now+50min, now+70min]` y el estado es `Open`: enviar notificación tipo 2 a miembros sin pronóstico de campeón.

Guardar en BD qué notificación ya se envió por sala para no repetirla (no en memoria — la app puede reiniciarse entre pings en F1).

### Criterios de aceptación

- [ ] Notificación "abierta" llega cuando finalizan todos los Dieciseisavos
- [ ] Notificación "cierre" llega ~60 min antes del primer Octavos
- [ ] Solo se envía a jugadores sin pronóstico de campeón activo
- [ ] No se repiten si ya se enviaron

---

## Módulo N6 — Nuevo cruce KO disponible para pronosticar

### Objetivo

Avisar cuando el admin asigna equipos reales a un partido placeholder (Octavos → Final), habilitando predicciones que antes no estaban disponibles.

### Mensaje

> 🆕 **Nuevo cruce disponible**  
> Brasil vs. Francia — Octavos de Final  
> Sala: Quiniela Amigos 2026

### Implementación

Hook al final de `KnockoutService.AssignTeamsAsync` (`src/Quiniela.Web/Services/KnockoutService.cs`). El partido ya tiene `HomeTeamId`/`AwayTeamId` asignados en ese punto — notificar a todos los miembros de las salas que incluyen ese partido.

```csharp
var members = await db.PoolMembers.Where(pm => pm.PoolId == poolId).ToListAsync();
foreach (var m in members)
    await pushSvc.SendAsync(m.UserId,
        "🆕 Nuevo cruce disponible",
        $"{homeTeam.ShortCode} vs. {awayTeam.ShortCode} — {stageLabel}",
        $"/pools/{poolId}/predictions");
```

### Criterios de aceptación

- [x] Notificación llega a todos los miembros de la sala cuando el admin asigna un cruce
- [x] Muestra los nombres/códigos de los dos equipos y la fase
- [x] El link lleva directamente a predicciones

---

## Módulo N7 — Todos los jugadores ya pronosticaron

### Objetivo

Generar expectativa social cuando el último miembro de la sala completa su pronóstico para un partido — todos tienen su predicción lista y nadie puede cambiarla por presión social.

### Mensaje

> 🎯 **¡Todos listos para México vs. Argentina!**  
> Todos en Quiniela Amigos 2026 ya pronosticaron — que gane el mejor.

### Implementación

Hook al final de `PredictionService.UpsertAsync` (`src/Quiniela.Web/Services/PredictionService.cs`). Después de guardar, revisar si todos los miembros de la sala tienen predicción para ese partido:

```csharp
var totalMembers = await db.PoolMembers.CountAsync(pm => pm.PoolId == poolId);
var withPrediction = await db.Predictions
    .CountAsync(p => p.MatchId == matchId && p.PoolId == poolId);

if (withPrediction == totalMembers)
{
    foreach (var m in poolMembers)
        await pushSvc.SendAsync(m.UserId,
            $"🎯 ¡Todos listos para {matchLabel}!",
            $"Todos en {pool.Name} ya pronosticaron.",
            $"/pools/{poolId}/predictions");
}
```

### Criterios de aceptación

- [x] Notificación se envía solo cuando el **último** miembro completa su pronóstico
- [x] No se envía si ya se había completado antes (cambio de pronóstico del último)
- [x] Incluye el nombre del partido y de la sala

---

## Módulo N8 — Partido comenzado

### Objetivo

Notificar a los jugadores cuando un partido arranca (el kickoff ya pasó), para que sigan el marcador en vivo y vean cómo van sus pronósticos contra los del resto de la sala.

### Mensaje

> 🔴 **¡Arrancó México vs. Argentina!**  
> El partido está en juego — mira los pronósticos de tu sala.

El link lleva a `/pools/{id}/predictions` si el usuario pertenece a una sola sala, o a `/pools` si pertenece a varias (mismo criterio que N2).

### Implementación

Es un evento **por tiempo** (el kickoff ocurre aunque nadie esté usando la app), por lo que la lógica vive en `NotificationCheckService` junto a N2 y N5, invocada desde el endpoint `/api/notify/check` que pingea la Azure Function cada 10 minutos:

```csharp
// src/Quiniela.Web/Services/NotificationCheckService.cs
private async Task CheckStartedMatchesAsync(QuinielaDbContext db, DateTime now)
{
    // Partidos cuyo kickoff cayó dentro de la ventana del último ping
    var startedMatches = await db.Matches
        .Where(m => m.KickoffUtc <= now && m.KickoffUtc > now.AddMinutes(-15)
                    && m.Status == MatchStatus.Programado
                    && m.HomeTeamId != null && m.AwayTeamId != null)
        .Include(m => m.HomeTeam).Include(m => m.AwayTeam)
        .ToListAsync();

    foreach (var match in startedMatches)
    {
        // usuarios miembros de alguna sala, excluyendo los (userId, matchId)
        // ya registrados en NotificationLog con Type = "MatchStarted"
        foreach (var userId in usersToNotify)
            await pushSvc.SendAsync(userId,
                $"🔴 ¡Arrancó {label}!",
                "El partido está en juego — mira los pronósticos de tu sala.",
                url);
    }
}
```

Notas:
- Ventana `[now - 15min, now]`: cubre el intervalo entre pings (10 min) con margen si un ping se retrasa o la app tarda en despertar (cold start F1). Solo se envían partidos con `KickoffUtc` ya pasado — nunca antes del arranque real.
- Como es evento de **partido** (no de sala), un usuario en varias salas recibe **una sola notificación** sin nombre de sala (ver Consideración de diseño: multi-sala).
- Deduplicación por `(userId, matchId, Type)` en `NotificationLog` — no en memoria, porque la app puede reiniciarse entre pings en F1.
- Solo se notifica si el partido tiene equipos reales asignados (los placeholders KO sin `HomeTeamId`/`AwayTeamId` se omiten).

### Criterios de aceptación

- [x] Notificación llega dentro de los ~10 min posteriores al kickoff (siguiente ping)
- [x] No se envía antes del kickoff
- [x] No se envía dos veces al mismo usuario por el mismo partido
- [x] Un usuario en varias salas recibe una sola notificación
- [x] Partidos placeholder KO sin equipos asignados se omiten
- [x] Usuarios sin suscripción activa se omiten silenciosamente

---

## Módulo N9 — Resumen diario

### Objetivo

Todos los días **a las 22:00 hora CDMX**, enviar a cada jugador un push "Aquí está tu resumen diario" con sus puntos del día y su movimiento en la tabla, enlazando al **Módulo L — Resumen diario** (`06_mejorasfases.md`) con la fecha por querystring. Solo se envía en días con al menos un partido finalizado.

**Depende del Módulo L** (la página destino debe existir primero).

### Mensaje

> 📅 **Tu resumen del 6 de julio**  
> +5 pts hoy · ⬆️ Subiste al 2° lugar  
> Sala: Quiniela Amigos 2026

Variantes del cuerpo según el caso:
- Con cambio de posición: `+5 pts hoy · ⬆️ Subiste al 2° lugar` / `⬇️ Bajaste al 4° lugar`
- Sin cambio: `+5 pts hoy · Sigues en 2° lugar`
- Sin pronósticos ese día: `Hoy no pronosticaste · Sigues en 3° lugar`

El link lleva a `/pools/{poolId}/daily-summary?date=2026-07-06` — por eso el Módulo L acepta la fecha por querystring.

### Categoría: evento de sala, disparado por tiempo (21:00 CDMX)

- Es un evento de **sala** (la posición depende de la sala): una notificación por sala, **incluye nombre de sala** (ver Consideración de diseño: multi-sala). Los puntos del día son iguales en todas las salas, pero la posición no.
- Es un evento **por tiempo**: se envía **una vez al día a las 22:00 hora CDMX** (decisión de producto), independientemente de si el admin ya capturó todos los resultados. La lógica vive en `NotificationCheckService` junto a N2/N5/N8, invocada por el ping de la Azure Function cada 10 minutos — en la práctica la notificación llega entre 22:00 y ~22:10.

### Implementación

Nuevo método en `NotificationCheckService`, registrado en `CheckAndNotifyAsync`:

```csharp
public async Task CheckAndNotifyAsync()
{
    await using var db = await dbFactory.CreateDbContextAsync();

    var now = DateTime.UtcNow;
    await CheckUpcomingMatchesAsync(db, now);   // N2
    await CheckStartedMatchesAsync(db, now);    // N8
    await CheckDailySummaryAsync(db, now);      // N9
}
```

Lógica de `CheckDailySummaryAsync`:

1. **¿Ya es hora?** Convertir `now` a America/Mexico_City. Si `localNow.Hour < 22`: return. (El primer ping después de las 22:00 dispara el envío; los siguientes quedan bloqueados por la dedup.)
2. **¿Hay algo que resumir?** Buscar los partidos **finalizados** cuyo `KickoffUtc` cae en el día local actual. Si no hay ninguno: return (día sin jornada o sin resultados capturados → no se envía nada).
3. **Deduplicación:** existe `NotificationLog` con `Type == "DailySummary"` para ese usuario cuyo `Match.KickoffUtc` cae en el mismo día local → ya se le envió hoy, omitir. La consulta es por día vía join a `Match` (no por `MatchId` exacto). Como `MatchId` de la fila de log se usa el **último partido finalizado del día** — siempre existe por el paso 2.
4. **Por cada sala, por cada miembro** (incluidos los que no pronosticaron ese día — decisión de producto):
   - Puntos del día = `SUM(Points)` de sus predicciones de los partidos del día en esa sala.
   - Posición actual = snapshot más reciente del pool (`GetLastSnapshotPositionsAsync` o equivalente acotado a `KickoffUtc <= fin del día`); posición previa = último snapshot anterior al inicio del día local (misma consulta que usa `SaveSnapshotAsync` para N3).
   - Armar cuerpo según variantes y enviar con URL `/pools/{poolId}/daily-summary?date={yyyy-MM-dd}`.
   - Registrar **una fila** `NotificationLog { UserId, MatchId = últimoPartidoDelDía, Type = "DailySummary" }` por usuario (el log no tiene `PoolId`; las notificaciones de todas sus salas se envían dentro de la misma corrida antes de registrar).

```csharp
private const string DailySummaryType = "DailySummary";
private static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
```

### Limitaciones aceptadas (por el corte fijo a las 22:00)

- **Partidos que terminan (o se capturan) después de las 22:00** no entran en la notificación de ese día y no provocan reenvío. La página del Módulo L sí los muestra siempre actualizados — la notificación es la invitación, la página es la verdad.
- Si la Function/app estuviera caída todo el tramo 22:00–23:59 (muy improbable con pings cada 10 min), el resumen de ese día no se envía; no se "recupera" al día siguiente.

### Relación con N3 (cambio de posición)

N3 ya notifica el cambio de posición **por partido**. En un día con 4 partidos, un jugador puede recibir varios N3 y a las 22:00 un N9 que consolida el día. Se decidió mantener ambos por ahora (N3 es inmediato y emocional, N9 es el cierre del día). Si en la práctica resulta ruidoso, la mejora futura es suprimir N3 y dejar solo N9 — se deja anotado como decisión reversible.

### Criterios de aceptación

- [x] La notificación llega entre las 22:00 y ~22:10 CDMX, **una sola vez al día** por usuario
- [x] Solo se envía si el día tiene al menos un partido finalizado (días sin jornada: silencio)
- [x] Llega una notificación por sala, con nombre de sala, puntos del día y posición (con dirección del cambio)
- [x] El link abre `/pools/{poolId}/daily-summary?date=yyyy-MM-dd` con el día correcto
- [x] Miembros sin pronósticos ese día también la reciben (variante "Hoy no pronosticaste")
- [x] Usuarios sin suscripción activa se omiten silenciosamente (comportamiento ya garantizado por `PushNotificationService`)

### Estimación: ~2 horas (una vez implementado el Módulo L)

---

## Módulo N11 — Anuncio one-shot: nuevos logros "Mejor/Peor del día"

### Objetivo

Anunciar **una sola vez** a todos los usuarios suscritos que existen 2 nuevos logros — 🔮 *Mejor del día* y 🪙 *Peor del día* (Módulos M1–M4 + N10, `08_insignias_mejorpeor.md`) — al momento de publicar la app en Azure. Es una notificación de marketing interno / hype, no ligada a ningún partido ni sala.

### Mensaje

Tono cómico/burlón (decisión de producto). El cuerpo **debe incluir literalmente** el texto `(si ves esto comparteme en whatsapp tu sticker mas puercote XD)`:

> 🚨 **Última hora**
> Nuevos logros: 🔮 Mejor del día y 🪙 Peor del día. Cada noche a las 9:30 se sabrá quién amaneció brujo… y quién la regó más. (si ves esto comparteme en whatsapp tu sticker mas puercote XD)

- El link lleva a `/pools/{poolId}/achievements` si el usuario pertenece a una sola sala, o a `/pools` si pertenece a varias (mismo criterio que N2/N8).
- Es un evento **global** (ni de partido ni de sala): un usuario recibe **una sola notificación** sin nombre de sala, sin importar en cuántas salas esté.

### Disparador: publicar en Azure

No se necesita mecanismo nuevo de deploy. La notificación viaja **dentro del código** del release: al publicar, la app se reinicia con la lógica de N11 incluida, y el **primer ping de la Azure Function** (`/api/notify/check`, cada 10 min) dispara el envío. En la práctica el anuncio llega dentro de los ~10 minutos posteriores al publish — suficiente para "al momento de publicar" sin tocar el pipeline.

Alternativa descartada: hook en `Program.cs` al arrancar. Enviaría unos minutos antes, pero en F1 la app arranca decenas de veces al día (cold start tras cada siesta de 20 min), lo que obligaría a consultar la dedup en cada arranque y mezcla responsabilidades — el patrón del proyecto es que todo lo disparado "por tiempo" vive en `NotificationCheckService`.

### Cambio de esquema: `MatchId` nullable en `NotificationLog`

`NotificationLog.MatchId` hoy es `int` no-nullable (FK a `Match`). Un anuncio global no tiene partido asociado:

```csharp
// src/Quiniela.Data/Entities/NotificationLog.cs
public int? MatchId { get; set; }      // era int
public Match? Match { get; set; }      // era Match = null!
```

Migración: `MakeNotificationLogMatchIdNullable`. Las filas existentes (N2/N8/N9/N10) no cambian; los tipos nuevos de anuncio guardan `MatchId = null`.

### Implementación

Nuevo método en `NotificationCheckService`, **primero** en `CheckAndNotifyAsync` (el anuncio no debe quedar detrás de los checks diarios):

```csharp
private const string AnnouncementType = "Announcement:mejor-peor-v1";
private static readonly DateTime AnnouncementExpiresUtc = new(2026, 7, 22); // ~2 semanas tras el release

public async Task CheckAndNotifyAsync()
{
    await using var db = await dbFactory.CreateDbContextAsync();

    var now = DateTime.UtcNow;
    await CheckAnnouncementsAsync(db, now);     // N11 (one-shot)
    await CheckUpcomingMatchesAsync(db, now);   // N2
    await CheckStartedMatchesAsync(db, now);    // N8
    await CheckDailyAwardsAsync(db, now);       // N10
    await CheckDailySummaryAsync(db, now);      // N9
}

private async Task CheckAnnouncementsAsync(QuinielaDbContext db, DateTime now)
{
    if (now >= AnnouncementExpiresUtc) return;  // ventana cerrada: check gratis para siempre

    // Usuarios suscritos que aún no reciben el anuncio
    var notified = await db.NotificationLogs
        .Where(l => l.Type == AnnouncementType)
        .Select(l => l.UserId)
        .ToListAsync();

    var pending = await db.PushSubscriptions
        .Where(s => !notified.Contains(s.UserId))
        .Select(s => s.UserId)
        .Distinct()
        .ToListAsync();

    foreach (var userId in pending)
    {
        await pushService.SendAsync(userId,
            "🚨 Última hora",
            "Nuevos logros: 🔮 Mejor del día y 🪙 Peor del día. Cada noche a las 9:30 " +
            "se sabrá quién amaneció brujo… y quién la regó más. " +
            "(si ves esto comparteme en whatsapp tu sticker mas puercote XD)",
            url); // /pools/{poolId}/achievements o /pools según nº de salas

        db.NotificationLogs.Add(new NotificationLog
        {
            UserId = userId, MatchId = null, Type = AnnouncementType, SentAt = now
        });
    }
    await db.SaveChangesAsync();
}
```

Notas:
- **Dedup por usuario en BD** (no un flag global): si la app muere a media corrida, el siguiente ping envía solo a los que faltan. Consistente con la regla transversal "no en memoria — F1 reinicia entre pings".
- **Solo usuarios ya suscritos al momento del release** reciben el anuncio. Quien se suscriba dentro de la ventana de 2 semanas también lo recibirá en el siguiente ping (efecto secundario aceptable — es bienvenida gratis); pasada la fecha de expiración, nadie más.
- **Fecha de expiración hardcodeada**: evita que el check corra eternamente y define el fin de la ventana. Ajustarla al día real del deploy + ~2 semanas.
- El sufijo `-v1` en el `Type` deja el mecanismo listo para futuros anuncios one-shot (nueva constante + nueva fecha, cero cambios de esquema).
- Silent fail y limpieza de suscripciones `410 Gone` ya los garantiza `PushNotificationService` (ver Notas transversales).

### Criterios de aceptación

- [x] Migración `MakeNotificationLogMatchIdNullable` aplicada sin afectar filas existentes
- [x] El anuncio llega a todos los usuarios suscritos dentro de los ~10 min posteriores al publish
- [x] Cada usuario lo recibe **una sola vez**, aunque la app se reinicie entre pings (dedup por usuario en BD, guardado por usuario dentro de la corrida)
- [x] El cuerpo incluye literalmente el texto `(si ves esto comparteme en whatsapp tu sticker mas puercote XD)`
- [x] El link lleva a la vitrina de logros (una sala) o a `/pools` (varias salas)
- [x] Pasada la fecha de expiración (2026-07-22 UTC), el check retorna de inmediato y no envía nada
- [x] Usuarios sin suscripción activa se omiten silenciosamente

### Estimación: ~1–2 horas

---

## Notas transversales de implementación

### Silent fail obligatorio

Todos los envíos push deben capturar `WebPushException`. Si el endpoint devuelve `410 Gone` (suscripción expirada), eliminar la fila de `PushSubscriptions` automáticamente. Nunca lanzar excepción al contexto llamador — las notificaciones son best-effort.

```csharp
try { await client.SendNotificationAsync(...); }
catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone)
    { await RemoveSubscriptionAsync(subscription.Endpoint); }
catch { /* ignorar otros errores de red */ }
```

### Límite de rate por usuario

No enviar más de 1 notificación por usuario cada 5 minutos para evitar spam. Como la app puede reiniciarse entre pings (F1 sin Always On), el control de duplicados se persiste en BD con una tabla `NotificationLog (UserId, MatchId, Type, SentAt)` — no en memoria.

### Payload máximo

Web Push soporta hasta ~4 KB de payload. El JSON `{title, body, url}` nunca se acercará a ese límite.

### Iconos

La notificación nativa usa `/icons/icon-192.png`. Si el proyecto no tiene ese ícono, basta con copiar el favicon escalado a 192×192 px en `wwwroot/icons/`.
