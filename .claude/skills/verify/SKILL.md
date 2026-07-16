---
name: verify
description: Cómo compilar, lanzar y verificar la app Quiniela en el navegador real (Playwright) y contrastar datos contra la BD dev.
---

# Verificar Quiniela (Blazor Server)

## Build + lanzar

```bash
dotnet build src/Quiniela.Web/Quiniela.Web.csproj -v q --nologo
dotnet run --project src/Quiniela.Web --no-build --urls http://localhost:5187   # en background
curl -s -o /dev/null -w "%{http_code}" http://localhost:5187/login              # 200 = listo (~8s)
```

Para detenerla (necesario antes de recompilar — el DLL queda bloqueado):

```powershell
Get-Process -Name Quiniela.Web -ErrorAction SilentlyContinue | Stop-Process -Force
```

## Config / credenciales

- Connection string y secretos vienen de `dotnet user-secrets` (proyecto Quiniela.Web), no de appsettings.
- BD dev: SQL Server `pc133\sqlexpress`, base `QuinielaDB`, Trusted_Connection. Cross-check con `sqlcmd -S "pc133\sqlexpress" -d QuinielaDB -E`.
- Usuarios seed: `admin`/`Admin12345` (rol Admin), `jugador1`..`jugadorN`/`Jugador12345`.
- Enums en BD: `MatchStatus.Finalizado = 1`, `MatchStage.Grupos = 0 … Final = 6`, `MatchDecidedIn.Penalties = 2`.
- Sala real de pruebas: PoolId = 1.

## Driving con Playwright

Playwright NO está en el repo: `npm init -y && npm install playwright` en el scratchpad (Chromium ya está instalado en el equipo). Script Node con `require('playwright')`.

- Login: `page.fill('#username', …)`, `page.fill('#password', …)`, `button[type="submit"]`.
- Páginas InteractiveServer: dar `waitForTimeout(~2000)` tras la navegación para que el circuito renderice.
- `/pools/{id}/final-summary`: la primera visita corre la ceremonia — clic en `button:has-text("Saltar")`. El flag de "visto" es localStorage por usuario+pool.
- Scroll-reveals (`.fs-reveal`): dispararlos con `page.mouse.wheel` en pasos; verificar `.fs-visible` y `[data-countup]` (el texto debe llegar al valor de `data-countup`).
- Probar también viewport 390px + `colorScheme: 'dark'`, y comprobar overflow horizontal con `scrollWidth > clientWidth`.

## Gotchas

- El banner "Vista previa" del admin es sticky y puede cruzar los screenshots de elemento: no es un bug.
- El gate del resumen final: no-admin ve "Disponible cuando termine el Mundial 🏆" mientras la Final no esté Finalizada.
