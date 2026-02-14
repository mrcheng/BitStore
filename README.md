# BitStore

BitStore hosts the modernized ASP.NET Core MVC app in:

- `BitStoreWeb/BitStoreWeb.Net9` (`.NET 9`)

The legacy `.NET Framework 4.5` / FubuMVC project has been removed.

## Quick Start (from repo root)

```powershell
npm --prefix BitStoreWeb/BitStoreWeb.Net9 install
npm --prefix BitStoreWeb/BitStoreWeb.Net9 run build
dotnet run --project BitStoreWeb/BitStoreWeb.Net9/BitStoreWeb.Net9.csproj
```

## Build

```powershell
npm --prefix BitStoreWeb/BitStoreWeb.Net9 run build
dotnet build BitStoreWeb/BitStoreWeb.Net9/BitStoreWeb.Net9.csproj
```

## Frontend Tooling

- Package manager: `npm`
- Bundler: `Vite`
- Frontend source: `BitStoreWeb/BitStoreWeb.Net9/frontend`
- Bundled output: `BitStoreWeb/BitStoreWeb.Net9/wwwroot/dist`

## Notes

- Solution file: `BitStoreWeb/BitStoreWeb.sln`
- Static assets are served from `BitStoreWeb/BitStoreWeb.Net9/wwwroot`
