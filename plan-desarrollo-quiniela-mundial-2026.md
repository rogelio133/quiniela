# Plan de Desarrollo — Quiniela Mundial 2026

## Contexto y alcance

Aplicación web para una quiniela entre amigos del Mundial FIFA 2026 (11 de junio – 19 de julio de 2026).

**Restricciones de diseño:**
- Sin manejo de dinero ni premios en efectivo.
- Autenticación: **usuario y contraseña** únicamente. No hay módulo de registro público; las cuentas se crean directamente en la base de datos (vía migración o script).
- Pronóstico por partido: solo **1X2** (gana local / empate / gana visitante). No se captura marcador.
- Carga de resultados: **manual**, hecha por un administrador.
- 104 partidos en total, distribuidos en 12 grupos de 4 + fase eliminatoria con ronda de dieciseisavos.
- **Sólo el administrador puede crear salas**; los demás usuarios únicamente se unen con código.

**Stack técnico:**
- .NET 10 (LTS, soportado hasta nov 2028) + ASP.NET Core
- Entity Framework Core
- SQL Server
- Blazor Server (recomendado para mantener todo en C#) o Web API + frontend separado
- `Microsoft.AspNetCore.Identity.PasswordHasher` (o BCrypt.Net) para el hash de contraseñas

---

## Ajustes al esquema por el alcance simplificado

Estas son las diferencias respecto al esquema original:

```sql
-- USERS: gestionado por ASP.NET Core Identity (tabla AspNetUsers)
-- IdentityUser<int> ya provee: Id, UserName, NormalizedUserName, PasswordHash, SecurityStamp, etc.
-- Propiedades extra que se agregan a la entidad User:
AspNetUsers (
    Id              INT IDENTITY   PRIMARY KEY,   -- hereda de IdentityUser<int>
    UserName        NVARCHAR(256)  NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(MAX)  NOT NULL,       -- PBKDF2 gestionado por Identity
    DisplayName     NVARCHAR(100)  NOT NULL,       -- propiedad personalizada
    IsAdmin         BIT            NOT NULL DEFAULT 0,  -- propiedad personalizada (sync con rol "Admin")
    CreatedAt       DATETIME2      NOT NULL        -- propiedad personalizada
    -- ...demás columnas estándar de Identity (NormalizedUserName, SecurityStamp, etc.)
)

-- TEAMS: selecciones participantes
TEAMS (
    Id              INT IDENTITY   PRIMARY KEY,
    Name            NVARCHAR(100)  NOT NULL,
    FlagCode        CHAR(2)        NOT NULL,       -- ISO 3166-1 alpha-2 en minúsculas (ej. "mx", "us", "ar")
    GroupCode       CHAR(1)        NULL            -- 'A'..'L', NULL para fases eliminatorias
    -- Uso en frontend: <span class="fi fi-@team.FlagCode"></span>
)

-- PREDICTIONS: sin marcador, solo el resultado
PREDICTIONS (
    Id              INT IDENTITY   PRIMARY KEY,
    UserId          INT            NOT NULL FK -> USERS,
    PoolId          INT            NOT NULL FK -> POOLS,
    MatchId         INT            NOT NULL FK -> MATCHES,
    PredOutcome     CHAR(1)        NOT NULL,    -- 'H', 'D', 'A'
    Points          INT            NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2      NOT NULL,
    UpdatedAt       DATETIME2      NOT NULL,
    CONSTRAINT UX_Prediction UNIQUE (UserId, PoolId, MatchId)
)

-- POOLS: regla de puntuación única (acertar 1X2)
POOLS (
    Id              INT IDENTITY   PRIMARY KEY,
    Name            NVARCHAR(100)  NOT NULL,
    JoinCode        NVARCHAR(8)    NOT NULL UNIQUE,
    OwnerId         INT            NOT NULL FK -> USERS,   -- siempre será el admin
    PtsCorrect      INT            NOT NULL DEFAULT 3,
    PtsBonusKO      INT            NOT NULL DEFAULT 2,  -- bonus por etapa eliminatoria
    CreatedAt       DATETIME2      NOT NULL
)

-- MATCHES: agregamos campo Stage como enum
MATCHES (
    Id              INT IDENTITY   PRIMARY KEY,
    HomeTeamId      INT            NULL FK -> TEAMS,   -- NULL hasta sorteo eliminatorio
    AwayTeamId      INT            NULL FK -> TEAMS,
    KickoffUtc      DATETIME2      NOT NULL,
    Stage           INT            NOT NULL,            -- enum: Grupos, Dieciseisavos, etc.
    GroupCode       CHAR(1)        NULL,                -- 'A'..'L' solo en fase de grupos
    HomeScore       INT            NULL,
    AwayScore       INT            NULL,
    Status          INT            NOT NULL DEFAULT 0   -- enum: Programado, Finalizado
)
```

---

## Módulo 0 — Setup del proyecto

**Objetivo:** Tener el esqueleto del proyecto corriendo localmente con conexión a BD.

**Tareas:**
- Crear solución .NET 10 con dos proyectos: `Quiniela.Web` (Blazor Server) y `Quiniela.Data` (EF Core).
- Configurar `appsettings.json` y `appsettings.Development.json` (sin secretos en repo).
- Configurar User Secrets para connection string y, en su caso, un *pepper* para el hash de contraseñas.
- Crear `QuinielaDbContext` con DbSets vacíos.
- Crear migración inicial vacía y aplicarla a la BD.
- Inicializar repositorio Git con `.gitignore` apropiado.

**Criterio de aceptación:** La aplicación arranca, conecta a SQL Server y muestra una página vacía sin errores.

**Estimación:** 4–6 horas.

---

## Módulo 1 — Autenticación con usuario y contraseña

**Objetivo:** Permitir el inicio de sesión con credenciales locales (usuario y contraseña) usando ASP.NET Core Identity. No existe registro público: las cuentas se siembran desde una migración (ver módulo 2).

**Implicaciones sobre el esquema:**
- La entidad `User` hereda de `IdentityUser<int>` para mantener `int` como tipo de PK.
- `QuinielaDbContext` hereda de `IdentityDbContext<User, IdentityRole<int>, int>` en lugar de `DbContext`.
- Identity crea sus propias tablas (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, etc.) vía migración. La columna `PasswordHash` y el campo `UserName` ya los provee `IdentityUser`; sólo hay que agregar `DisplayName` e `IsAdmin` como propiedades extras.
- El campo `IsAdmin` se mantiene en `User` como conveniencia, pero el control de acceso real se delega a Identity Roles: al crear el usuario en seed se le asigna el rol `"Admin"` si corresponde. Esto permite usar `[Authorize(Roles = "Admin")]` de forma nativa.

**Tareas:**
- Instalar `Microsoft.AspNetCore.Identity.EntityFrameworkCore`.
- Definir la entidad `User : IdentityUser<int>` con las propiedades extra `DisplayName` y `IsAdmin`.
- Actualizar `QuinielaDbContext` para heredar de `IdentityDbContext<User, IdentityRole<int>, int>`.
- Registrar Identity en `Program.cs`:
  ```csharp
  builder.Services.AddIdentity<User, IdentityRole<int>>(options => {
      options.Password.RequireNonAlphanumeric = false;
      options.Password.RequireUppercase = false;
      options.User.RequireUniqueEmail = false;
  })
  .AddEntityFrameworkStores<QuinielaDbContext>()
  .AddDefaultTokenProviders();
  ```
- Configurar la cookie de Identity en `Program.cs`:
  - Tiempo de expiración de 30 días con *sliding expiration*.
  - `LoginPath = "/login"`, `LogoutPath = "/logout"`.
  - Cookie `HttpOnly`, `Secure` y `SameSite=Lax`.
- Generar y aplicar la migración que crea las tablas de Identity.
- Crear página `/login` con formulario: campo *Usuario*, campo *Contraseña*, botón *Entrar*.
- Handler de login que usa `SignInManager<User>`:
  1. Llama a `SignInManager.PasswordSignInAsync(username, password, isPersistent: true, lockoutOnFailure: false)`.
  2. Si el resultado es `Succeeded`, redirige al home.
  3. Si falla (cualquier resultado), muestra error genérico ("Usuario o contraseña inválidos") sin distinguir si el usuario existe.
- Handler de logout que llama a `SignInManager.SignOutAsync()` y redirige a `/login`.
- Layout con `DisplayName` del usuario (desde `UserManager.GetUserAsync(User)`) y botón de cerrar sesión.
- Atributo `[Authorize]` en todas las rutas que requieran login.
- Atributo `[Authorize(Roles = "Admin")]` en las rutas administrativas.
- **No se implementa pantalla de registro, recuperación de contraseña, ni cambio de contraseña** en esta versión.

**Criterio de aceptación:** Un usuario con cuenta pre-cargada puede iniciar sesión; usuarios inexistentes o con contraseña incorrecta reciben el mismo mensaje de error; las rutas protegidas redirigen a `/login` si no hay sesión activa; las rutas de admin devuelven 403 a usuarios sin rol `Admin`.

**Estimación:** 5–7 horas.

**Riesgos:**
- Identity deshabilita el lockout por defecto si `lockoutOnFailure: false`; para un login de amigos esto es aceptable, pero si se quiere añadir protección contra fuerza bruta hay que habilitar `lockoutOnFailure: true` y configurar `options.Lockout`.
- Sin "olvidé mi contraseña", si un usuario pierde su clave, el admin deberá regenerarla usando `UserManager.ResetPasswordAsync()` con un token generado por consola, o actualizando el hash directamente mediante un script.

---

## Módulo 2 — Datos base del torneo (seed)

**Objetivo:** Cargar las 48 selecciones (con banderas), los 72 partidos de fase de grupos y los usuarios iniciales en la BD.

### Prerequisito: columna FlagCode en Teams

Antes de crear el seed, la entidad `Team` incluye `FlagCode CHAR(2) NOT NULL` — el código ISO 3166-1 alpha-2 en minúsculas. Ya está incorporado en la entidad y configurado en `QuinielaDbContext`. Hay que generar la migración correspondiente:

```bash
dotnet ef migrations add AddTeamFlagCode --project src/Quiniela.Data --startup-project src/Quiniela.Web
dotnet ef database update --project src/Quiniela.Data --startup-project src/Quiniela.Web
```

### Integración de banderas con flag-icons

La librería `flag-icons` ya está referenciada vía CDN en `App.razor`. Uso en cualquier componente Razor:

```razor
<span class="fi fi-@team.FlagCode.ToLower()" title="@team.Name"></span>
```

El CDN apunta a `flag-icons@7.2.3` (jsdelivr). Si se prefiere sin dependencia de CDN, se puede instalar el paquete npm y copiar los assets a `wwwroot/lib/flag-icons/`.

### Tareas

- Generar y aplicar la migración `AddTeamFlagCode` (ver arriba).
- Crear seeder en `OnModelCreating` o script de inicialización que solo corra si las tablas están vacías.
- Cargar las 48 selecciones con su grupo asignado (A–L) y su `FlagCode` ISO alpha-2, conforme al sorteo oficial de la FIFA.
  - Referencia rápida de códigos: `ar`=Argentina, `br`=Brasil, `mx`=México, `us`=EE.UU., `ca`=Canadá, `fr`=Francia, `de`=Alemania, `es`=España, `pt`=Portugal, `gb-eng`=Inglaterra, `nl`=Países Bajos, `hr`=Croacia, `ma`=Marruecos, `sn`=Senegal, `jp`=Japón, `au`=Australia, etc.
  - Para selecciones con código especial en `flag-icons` (Gales=`gb-wls`, Inglaterra=`gb-eng`, Escocia=`gb-sct`), usar el código extendido IANA, que la librería soporta.
- Cargar los 72 partidos de fase de grupos con `HomeTeamId`, `AwayTeamId`, `KickoffUtc` (en UTC), `Stage = Grupos`, `GroupCode`.
- Los 32 partidos de eliminatorias se crearán dinámicamente al cerrar la fase de grupos (módulo 8).
- Confirmar que las fechas en UTC son consistentes con los horarios oficiales de la FIFA.
- **Crear migración `SeedInitialUsers` que inserte 3 usuarios:**
  - **1 administrador** con `IsAdmin = 1` (ej. `Username = "admin"`).
  - **2 usuarios normales** con `IsAdmin = 0` (ej. `Username = "jugador1"`, `Username = "jugador2"`).
  - Los `PasswordHash` deben generarse de antemano con el mismo algoritmo que usará el módulo 1 (PBKDF2 o BCrypt). **No incrustar contraseñas en texto plano en la migración.**
  - Sugerencia: generar los hashes con un pequeño programa de consola y pegarlos en la migración; documentar las contraseñas iniciales en un lugar seguro (gestor de contraseñas) y cambiarlas tras el primer uso si así se decide.

**Criterio de aceptación:**
- Una consulta `SELECT COUNT(*) FROM MATCHES WHERE Stage = 0` devuelve 72 y todos tienen ambos equipos asignados.
- Una consulta `SELECT COUNT(*) FROM Teams` devuelve 48, y cada fila tiene `FlagCode` de 2 caracteres.
- La bandera de cada selección se renderiza correctamente en el componente Razor (`<span class="fi fi-@team.FlagCode">`) sin errores 404 en la CDN.
- Una consulta sobre la tabla de usuarios devuelve 3 registros, exactamente uno con `IsAdmin = 1`.
- Los 3 usuarios pueden iniciar sesión con sus contraseñas iniciales.

**Estimación:** 6–8 horas (la mayor parte es transcribir el fixture oficial con los `FlagCode`; +1 hora por la generación segura de hashes y la migración de usuarios).

---

## Módulo 3 — Salas (Pools)

**Objetivo:** Permitir que el administrador cree salas y que cualquier usuario se una con un código.

**Tareas:**
- Pantalla "Mis salas" listando las salas en las que participa el usuario.
- Botón "Crear sala": **visible y accesible únicamente para usuarios con `IsAdmin = 1`**. Pide nombre, genera `JoinCode` de 6 caracteres alfanuméricos único, registra al admin como miembro y owner.
  - En servidor: el endpoint de creación verifica `User.IsInRole("Admin")` (o equivalente) y rechaza con 403 si no se cumple. La UX (ocultar el botón) es complementaria, no reemplazo de la validación.
- Botón "Unirme a sala": disponible para **todos los usuarios autenticados** (incluido el admin). Pide código, valida y crea registro en `POOL_MEMBERS`.
- Pantalla de configuración (solo owner = admin): editar nombre, ver código de invitación con botón "copiar al portapapeles".
- Pantalla pública de la sala con lista de miembros.

**Criterio de aceptación:**
- Un usuario normal no ve el botón "Crear sala" y, si intenta llamar al endpoint manualmente, recibe 403.
- El admin puede crear salas y compartir el código.
- Cualquier usuario (admin o no) puede unirse a una sala con el código.
- El owner puede ver la configuración de la sala; los miembros no.

**Estimación:** 6–8 horas.

---

## Módulo 4 — Pronósticos (1X2)

**Objetivo:** Captura simple del pronóstico para cada partido futuro.

**Tareas:**
- Vista "Próximos partidos" filtrada por sala, mostrando partidos no iniciados.
- Por cada partido: nombre de los dos equipos, fecha/hora local del usuario, tres botones grandes: **Local**, **Empate**, **Visitante**.
- Al pulsar un botón, se hace `upsert` de la predicción.
- Validación en servidor: si `Kickoff <= UtcNow`, rechazar la operación.
- Indicador visual del pronóstico ya guardado y del tiempo restante hasta el bloqueo.
- Vista "Mis pronósticos" agrupada por jornada con el resultado real una vez finalizado el partido.

**Criterio de aceptación:** Un usuario puede registrar y cambiar su pronóstico tantas veces como quiera antes del kickoff; una vez iniciado el partido, el botón se desactiva y la BD rechaza cualquier escritura.

**Estimación:** 8–12 horas (la UI es donde se va más tiempo).

**Notas técnicas:**
- Mostrar fecha y hora con `TimeZoneInfo` y la zona del navegador (`Intl.DateTimeFormat().resolvedOptions().timeZone`).
- En Blazor Server, usar `IJSRuntime` para obtener la zona horaria del cliente al cargar la página.

---

## Módulo 5 — Panel de administración (carga manual)

**Objetivo:** Que tú (admin) puedas capturar los resultados rápido conforme terminan los partidos.

**Tareas:**
- Ruta `/admin` protegida con `[Authorize(Roles = "Admin")]` (o verificación equivalente sobre `IsAdmin = 1`).
- Vista "Partidos por capturar": lista de partidos cuyo `Kickoff < UtcNow` y `Status != Finalizado`.
- Formulario inline por fila: dos campos numéricos (HomeScore, AwayScore) y botón "Guardar".
- Al guardar:
  1. Validar que ambos goles sean ≥ 0.
  2. Actualizar `HomeScore`, `AwayScore`, `Status = Finalizado`.
  3. Disparar recálculo de puntos para todas las predicciones de ese partido (módulo 6).
- Botón "Editar" en partidos ya capturados, en caso de error de captura.
- Vista de auditoría: ver qué partidos están finalizados.

**Criterio de aceptación:** Solo el admin puede ver y operar esta pantalla; capturar un resultado actualiza los puntos de todos los pronósticos de ese partido en una sola operación.

**Estimación:** 6–8 horas.

---

## Módulo 6 — Motor de puntuación

**Objetivo:** Calcular y persistir los puntos de cada predicción al cargarse el resultado.

**Tareas:**
- Servicio `ScoringService` con método `RecalculateForMatch(int matchId)`.
- Algoritmo:
  1. Obtener el partido y su outcome real: `H` si HomeScore > AwayScore, `A` si menor, `D` si iguales.
  2. Obtener todas las `PREDICTIONS` para ese MatchId, agrupadas por PoolId (porque cada sala puede tener reglas distintas).
  3. Para cada predicción, comparar `PredOutcome` contra el outcome real.
  4. Si coincide, asignar `Points = PtsCorrect` (más `PtsBonusKO` si `Stage != Grupos`).
  5. Si no coincide, `Points = 0`.
  6. Guardar en una sola transacción.
- Pruebas unitarias con casos: acierto, fallo, partido sin pronóstico, predicción tardía (no debería existir pero defensa en profundidad).

**Criterio de aceptación:** Tras capturar un resultado, las predicciones de ese partido tienen `Points` actualizado correctamente; volver a editar el resultado recalcula sin duplicar.

**Estimación:** 4–6 horas.

---

## Módulo 7 — Tabla de posiciones

**Objetivo:** Mostrar el ranking de cada sala en tiempo casi real.

**Tareas:**
- Vista "Tabla de posiciones" por sala.
- Query: `SELECT UserId, SUM(Points) AS Total, COUNT(CASE WHEN Points > 0 THEN 1 END) AS Aciertos FROM PREDICTIONS WHERE PoolId = @id GROUP BY UserId ORDER BY Total DESC, Aciertos DESC`.
- Join con USERS para mostrar `DisplayName`.
- Columnas: posición, jugador, total de puntos, partidos acertados, total de pronósticos hechos.
- Refresco automático al recargar la página (en v2 puede ser SignalR para vivo).

**Criterio de aceptación:** Después de capturar un resultado, al recargar la tabla, el ranking refleja los cambios; en caso de empate de puntos, gana quien tiene más aciertos absolutos.

**Estimación:** 4–6 horas.

---

## Módulo 8 — Fase eliminatoria

**Objetivo:** Habilitar pronósticos para los 32 partidos de fase eliminatoria una vez terminada la fase de grupos.

**Tareas:**
- Función "Cerrar fase de grupos" en admin: cuando todos los partidos de grupos están finalizados, calcular:
  - Los dos primeros lugares de cada grupo (12 × 2 = 24 selecciones).
  - Los 8 mejores terceros lugares (criterios oficiales FIFA: puntos, diferencia de goles, goles a favor).
- Generar los 16 partidos de dieciseisavos según las llaves oficiales de la FIFA.
- Conforme avanza el torneo, generar octavos, cuartos, semifinales, tercer lugar y final, cada uno cuando se conocen sus participantes.
- Vista bracket para visualizar el cuadro.

**Criterio de aceptación:** Al finalizar el último partido de grupos y cerrar la fase, aparecen automáticamente los 16 partidos de dieciseisavos con sus dos equipos asignados, y los usuarios pueden empezar a pronosticarlos.

**Estimación:** 12–16 horas (la lógica de los 8 mejores terceros es la parte compleja; se recomienda escribirla con pruebas unitarias).

**Nota:** Este módulo se puede dejar para después del kickoff del Mundial; tienes hasta el 4 de julio (primer día de dieciseisavos) para tenerlo listo.

---

## Módulo 9 — Mejoras post-lanzamiento (v2)

Nada de lo siguiente bloquea el lanzamiento, pero suma mucho si hay tiempo:

- **Cambio de contraseña** desde el perfil del usuario y/o **reset de contraseña** desde el panel de admin.
- **Notificaciones por correo:** recordatorio 2 horas antes de la siguiente jornada para quienes no hayan pronosticado todos sus partidos. Usar `BackgroundService` + SendGrid o SMTP gratuito (requiere agregar un campo `Email` opcional a `USERS`).
- **Estadísticas personales:** porcentaje de aciertos, mejor racha, ranking histórico.
- **Leaderboard en vivo con SignalR:** la tabla se actualiza sin recargar cuando se captura un resultado.
- **Modo "predicciones cerradas, partido en vivo":** ver qué pronosticó cada quien antes del kickoff.
- **Logs de actividad** y respaldo automatizado de la BD.

---

## Cronograma sugerido

Asumiendo trabajo de pasatiempo, ~10 horas por semana, arrancando ya:

| Semana | Módulos | Entregable |
|--------|---------|------------|
| 1 | 0, 1 | Login con usuario/contraseña funcionando |
| 2 | 2, 3 | Datos cargados (incluidos usuarios iniciales), salas operativas (sólo admin crea) |
| 3 | 4 | Pronósticos capturables |
| 4 | 5, 6, 7 | Admin + scoring + tabla de posiciones |
| 5 | Pruebas, ajustes, despliegue | Aplicación lista para fase de grupos |
| 6+ | Módulo 8 | Eliminatorias (antes del 4 de julio) |

**Hito crítico:** Módulos 0–7 deben estar listos antes del **11 de junio de 2026** (kickoff del primer partido).

---

## Notas técnicas transversales

**Zonas horarias.** Los partidos del Mundial 2026 se juegan en 3 países (México, EE.UU., Canadá) abarcando varios husos horarios. Almacenar siempre `KickoffUtc` en UTC y convertir a la zona del usuario en el navegador.

**Validación de bloqueo.** El cierre del pronóstico al iniciar el partido debe validarse **siempre en el servidor**, nunca solo en el cliente. La UI bloquea por UX, pero la API/handler rechaza con 403 si `Kickoff <= UtcNow`.

**Migraciones y respaldos.** Antes del 11 de junio, hacer un respaldo completo de la BD. Durante el torneo, respaldo diario automático.

**Despliegue.** Opciones económicas: Azure App Service (plan B1 o gratuito si entran pocos usuarios), Railway, o un VPS con Docker. SQL Server puede ser Azure SQL Database (tier básico) o SQL Server Express en el mismo VPS.

**Seguridad mínima:**
- HTTPS obligatorio en producción (las cookies de sesión viajan en cada request).
- **Hashing de contraseñas** con PBKDF2 (ASP.NET default) o BCrypt; nunca SHA-256 simple ni texto plano.
- Antiforgery tokens en todos los formularios, en especial el de login.
- Rate limiting en el endpoint de login (ej. 5 intentos por IP por minuto) y en el de guardar pronóstico.
- Mensaje genérico de error en login para no revelar si el usuario existe.
- No exponer `IsAdmin` ni `PasswordHash` en respuestas que vean usuarios no admin (idealmente nunca exponer `PasswordHash`).

---

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|--------|------------|
| Olvidar capturar un resultado en vivo | Recordatorio en la pantalla de admin con conteo de partidos pendientes |
| Caída del servidor en horario de pronósticos | Despliegue en plataforma con SLA + monitoreo básico (UptimeRobot) |
| Confusión con horarios al haber 3 países anfitriones | Mostrar siempre la zona horaria detectada del usuario y un tooltip con la zona local del estadio |
| Lógica de "mejores terceros" incorrecta | Cobertura con pruebas unitarias y revisión cruzada con tabla oficial de FIFA |
| Usuario edita pronóstico justo en el kickoff (race condition) | Validación de `Kickoff > UtcNow` dentro de la transacción de guardado |
| Usuario olvida su contraseña y no hay flujo de recuperación | El admin regenera el hash con un script puntual y comparte la nueva contraseña por canal privado |
| Ataque de fuerza bruta sobre el login | Rate limiting por IP y por usuario; bloqueo temporal tras N intentos fallidos |

---

## Próximos pasos inmediatos

1. Crear la solución y arrancar el módulo 0.
2. Definir el algoritmo de hashing y generar los `PasswordHash` para el admin y los 2 usuarios iniciales (módulo 2 depende de esto).
3. Conseguir el fixture oficial de los 72 partidos de grupos en formato estructurado (CSV/JSON) para el seed del módulo 2.
