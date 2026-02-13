# AGENTS.md

Guidance for coding agents in this repository.

## Active Project

- Main app: `BitStoreWeb.Net9`
- Framework: `ASP.NET Core MVC` on `.NET 9`
- Legacy FubuMVC project has been removed.

## Where To Work

- Controllers: `BitStoreWeb.Net9/Controllers`
- Views: `BitStoreWeb.Net9/Views`
- Static assets: `BitStoreWeb.Net9/wwwroot`
- App startup: `BitStoreWeb.Net9/Program.cs`

## Build And Run

```powershell
dotnet build BitStoreWeb.Net9/BitStoreWeb.Net9.csproj
dotnet run --project BitStoreWeb.Net9/BitStoreWeb.Net9.csproj
```

## Conventions

- Keep changes focused and minimal.
- Prefer preserving existing routes and page behavior unless a refactor is requested.
- Avoid introducing legacy package systems or non-SDK project files.
