# ShiftPlanner — IIS Deployment Guide

This guide covers deploying `web/api` (which also serves the built `web/client`
SPA as static files — see [Architecture](#architecture-recap) below) to IIS on
Windows Server or Windows 10/11. It does not cover the Mobile app (Android/
MAUI) or the frozen Desktop prototype — Mobile just needs to be pointed at
whatever public URL you give this deployment (see [Mobile](#pointing-mobile-at-this-deployment)).

## Architecture recap

One ASP.NET Core app (`web/api`, target framework `net10.0`) serves both the
JSON API and the compiled React SPA. `web/client`'s Vite build is configured
to output directly into `web/api/wwwroot` (`vite.config.ts` →
`build.outDir: '../api/wwwroot'`), and `Program.cs` calls
`app.UseDefaultFiles()` / `app.UseStaticFiles()` ahead of the API routes. So
there is **one site to stand up in IIS**, not two — you build the SPA first,
then publish the API project, and its `wwwroot` carries the SPA along with it.

The API uses SQLite (`web/api/shiftplanner.db` by default, via the
`ConnectionStrings:Default` setting) and ASP.NET Core Identity with bearer
tokens (no cookies), so there's no session-affinity requirement if you ever
scale beyond one instance — though SQLite itself is single-writer and not
built for that; see [Database notes](#database-notes).

## Prerequisites (on the IIS server)

1. **IIS** with the **Web Server (IIS)** role enabled, including the
   **ASP.NET Core Module** — installed via the hosting bundle below, not a
   separate Windows feature.
2. **.NET 10 Hosting Bundle** (not just the SDK/runtime) —
   [download](https://dotnet.microsoft.com/download/dotnet/10.0), pick the
   "Hosting Bundle" installer for Windows. This installs the ASP.NET Core
   Runtime, the ASP.NET Core Module v2 (ANCM) for IIS, and registers it with
   IIS. **Restart IIS** (`iisreset`) or the server after installing.
3. Confirm it registered correctly:
   ```powershell
   Get-ChildItem "$env:windir\System32\inetsrv\config\schema" -Filter "aspnetcore_schema*.xml"
   ```
   If that returns a file, ANCM is installed.

On the **build machine** (can be the same box or your dev machine — you ship
the published output, not source):
- .NET 10 SDK (`dotnet --list-sdks` should show a `10.x` entry)
- Node.js (to build `web/client`) — whatever version `web/client/package.json`
  expects (React 19 + Vite 6 era; Node 20+ is safe)

## 1. Build the SPA into `wwwroot`

```powershell
cd web/client
npm ci
npm run build
```

`npm run build` runs `tsc -b && vite build`; Vite's `emptyOutDir: true`
means this **wipes and repopulates** `web/api/wwwroot` on every build — don't
hand-edit anything under there.

## 2. Publish the API

From the repo root:

```powershell
dotnet publish web/api/ShiftPlanner.Api.csproj -c Release -o publish/
```

(`publish/` is already in `.gitignore`.) This produces a self-contained
publish folder containing the compiled API, its dependencies, the `wwwroot`
folder you just built (Sdk.Web projects publish `wwwroot` automatically), and
a generated `web.config` that wires up the ASP.NET Core Module — you don't
need to hand-write one.

Copy the contents of `publish/` to the server, e.g. `C:\inetpub\shiftplanner\`.

## 3. Configure production settings

`appsettings.json` ships with:

```json
{
  "ConnectionStrings": { "Default": "Data Source=shiftplanner.db" }
}
```

That's a **relative path**, resolved against the app's working directory
(which IIS/ANCM sets to the site's physical path) — so on the server the db
file will live at `C:\inetpub\shiftplanner\shiftplanner.db` by default. That's
fine for a single self-hosted team; if you want it elsewhere (e.g. a data
drive that survives redeploys), add an `appsettings.Production.json` next to
`appsettings.json` on the server:

```json
{
  "ConnectionStrings": { "Default": "Data Source=D:\\ShiftPlannerData\\shiftplanner.db" }
}
```

(`appsettings.Production.json` is picked up automatically once
`ASPNETCORE_ENVIRONMENT=Production` — see next section — because ASP.NET
Core's config layering loads `appsettings.{Environment}.json` over the base
file.)

Do **not** deploy `appsettings.Development.json` or rely on it — it's for
local dev only and isn't part of the publish output regardless.

## 4. Create the IIS site

1. **Application Pool**: create one dedicated to this app (don't reuse an
   existing .NET Framework pool).
   - **.NET CLR version: No Managed Code** — ASP.NET Core doesn't run inside
     the IIS CLR host; ANCM forwards to the out-of-process/in-process
     Kestrel-backed app instead, so the pool's CLR setting is irrelevant to
     it and must be set to "No Managed Code" to avoid IIS trying (and
     failing) to load it as a classic .NET Framework app.
   - **Identity**: `ApplicationPoolIdentity` is fine (default) — see the file
     permissions note below.
2. **Site**: point the physical path at your publish folder
   (`C:\inetpub\shiftplanner\`), bind it to the app pool from step 1, and set
   up your binding (port 80/443, hostname, and an SSL certificate if you're
   terminating TLS at IIS — recommended, since `Program.cs` calls
   `app.UseHttpsRedirection()`).
3. **Environment variable**: set `ASPNETCORE_ENVIRONMENT=Production` for the
   site. Easiest way — add it to the generated `web.config`'s
   `<aspNetCore>` element:
   ```xml
   <aspNetCore processPath="dotnet" arguments=".\ShiftPlanner.Api.dll" ...>
     <environmentVariables>
       <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
     </environmentVariables>
   </aspNetCore>
   ```
   (If you skip this, it defaults to `Production` anyway per ASP.NET Core's
   own default when the variable is unset — but setting it explicitly avoids
   any ambiguity and matches what `appsettings.Production.json` expects.)

## 5. File permissions (important — SQLite needs write access)

SQLite writes directly to the `.db` file (and creates `-shm`/`-wal` sidecar
files under WAL mode) in the **same directory** as the connection string
points to. Grant the app pool's identity **Modify** permission on that
folder:

```powershell
$poolIdentity = "IIS AppPool\YourAppPoolName"
icacls "C:\inetpub\shiftplanner" /grant "${poolIdentity}:(OI)(CI)M"
```

If you pointed the connection string at a separate data folder (step 3),
grant the same permission there instead of/in addition to the site folder.
Without this, the app will start (reads/static files work) but any write —
including the very first seed on startup — will fail.

## 6. Start it and verify

```powershell
Start-Website -Name "ShiftPlanner"   # or use IIS Manager
```

- Browse to the site's URL — you should get the ShiftPlanner login screen
  (served from `wwwroot/index.html`), not a raw IIS 404.
- Hit `https://your-site/api/teams/mine` in a browser while unauthenticated —
  you should get a `401`, not a `502.5`/`500.30` (those indicate ANCM
  couldn't start the app; check the **Windows Event Log → Application** and
  `C:\inetpub\shiftplanner\logs\stdout*.log` if you enabled stdout logging in
  `web.config`).
- Sign up a first account via the app's "Create account" flow, then create a
  team — `DbSeeder` only seeds a demo team/admin on a **fresh empty
  database**, so a first-run production deploy starts blank, not with the
  dev seed data.

## Database notes

- The app calls `EnsureCreated()` on startup (see `Program.cs` /
  `DbSeeder.SeedAsync`), **not** EF Core migrations. It will create the
  schema if the `.db` file doesn't exist yet, but it will **not** apply
  schema changes to an existing database on a later redeploy. If you ship a
  version with model changes, you currently need to either start from a
  fresh `.db` file or apply the equivalent schema change by hand — there is
  no migration pipeline yet.
- **Back up `shiftplanner.db` before every redeploy.** Stopping the app pool
  first (`Stop-WebAppPool`) avoids copying a file mid-write.
- SQLite is single-writer. This is fine for one team's self-hosted instance;
  it is **not** a suitable backing store if you intend to run this as a
  multi-instance/load-balanced SaaS deployment — you'd want to swap the
  `UseSqlite(...)` call in `Program.cs` for a real server (SQL Server/
  PostgreSQL via the matching EF Core provider) before scaling that way.

## Redeploying an update

1. `Stop-WebAppPool -Name "YourAppPoolName"` (releases the file locks and
   stops writes to the db while you copy).
2. Back up `shiftplanner.db`.
3. Rebuild (`npm run build` in `web/client`, then `dotnet publish` as above).
4. Copy the new publish output over the old one, **excluding** `shiftplanner.db`
   (and any `-shm`/`-wal` files) so you don't overwrite live data with an
   empty/dev one.
5. `Start-WebAppPool -Name "YourAppPoolName"`.

## Pointing Mobile at this deployment

The MAUI app has no baked-in server address — on first run (or from
**Profile → API Server Address**) a user enters the deployment's public URL,
e.g. `https://shiftplanner.yourcompany.com`. No mobile-specific server
configuration is needed; it talks to the same `/api/...` routes as the SPA.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `HTTP 500.19` | Missing/invalid `web.config` — re-run `dotnet publish`, don't hand-edit the generated one beyond adding the environment variable. |
| `HTTP 500.30 — ANCM in-process start failure` | Hosting Bundle not installed, or installed after IIS started (run `iisreset`). Check `logs\stdout*.log`. |
| `HTTP 502.5 — ANCM out-of-process` | Same as above, or the published `.dll`/`processPath` in `web.config` doesn't match what's actually on disk. |
| App loads but every write (login, create team) fails | App pool identity lacks write permission on the folder holding `shiftplanner.db` — see [File permissions](#5-file-permissions-important--sqlite-needs-write-access). |
| SPA loads but shows a blank page / 404s on refresh at a route like `/roster` | `app.UseDefaultFiles()`/`UseStaticFiles()` order issue would be a code bug, not a deploy one — first confirm `wwwroot/index.html` and `wwwroot/assets/*` actually made it into the publish output; if so this is a React Router deep-link issue, not related to this guide. |
| CORS errors in the browser console | Only happens if the SPA is somehow being served from a different origin than the API — in this deployment model they're the same origin, so you shouldn't see this. If you do, double-check you didn't accidentally point the site at `web/client`'s own dev output instead of the API's `wwwroot`. |
