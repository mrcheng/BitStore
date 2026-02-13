# AGENTS.md

Guidance for coding agents working in `BitStoreWeb`.

## Mission

Make focused, low-risk improvements to a legacy `FubuMVC` + `.NET Framework 4.5` web app without breaking existing conventions.

## Quick Context

- This is a Visual Studio web application project (`BitStoreWeb.csproj`), not SDK-style.
- Startup is configured in `App_Start/FubuMVC.cs` via `WebActivator`.
- `ConfigureFubuMVC.cs` contains the root Fubu registry and sets the home route.
- Endpoints are organized by feature under `Endpoints/*` with matching `InputModel`, `ViewModel`, and `.cshtml`.
- Many current endpoint actions are thin and return empty view models; views hold most of the visible behavior.

## Conventions To Preserve

- Keep `TargetFrameworkVersion` at `v4.5` unless explicitly asked to migrate.
- Keep Fubu naming conventions (`get_*` methods, input/view model pairing) unless route refactoring is requested.
- Keep layout and navigation wiring through `Shared/Application.cshtml`.
- Prefer minimal, additive edits over framework-wide rewrites.

## Where To Change What

- New or changed page behavior: add or edit endpoint classes in `Endpoints/<Feature>/`.
- New or changed page markup: add or edit corresponding `.cshtml` views in the same feature folder.
- App bootstrapping and route policy: `App_Start/FubuMVC.cs` and `ConfigureFubuMVC.cs`.
- Global styling and JavaScript: `Content/Site.css` and `Scripts/script.js`.

## Build And Verify

Run from repo root:

```powershell
msbuild BitStoreWeb.sln /t:Build /p:Configuration=Debug
```

If NuGet references are missing:

```powershell
nuget restore BitStoreWeb.sln
```

After code edits, do a smoke pass of these pages:

- `/`
- `/buckets`
- `/bucket`
- `/howto`
- `/api`

## Risk Notes

- Dependencies are old and tightly coupled; avoid unnecessary package upgrades.
- Frontend assets include multiple library versions. Reconcile carefully before removing anything.
- There are no automated tests in this repo, so always include manual verification notes in handoff.
