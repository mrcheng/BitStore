# BitStore

This repository now hosts the modernized ASP.NET Core MVC app at:

- `BitStoreWeb.Net9` (`.NET 9`)

The legacy `.NET Framework 4.5` / FubuMVC project has been removed.

## Run

```powershell
cd BitStoreWeb.Net9
npm install
npm run build
dotnet run
```

## Build

```powershell
cd BitStoreWeb.Net9
npm run build
dotnet build BitStoreWeb.Net9/BitStoreWeb.Net9.csproj
```

## Frontend Tooling

- Package manager: `npm`
- Bundler: `Vite`
- Frontend source: `BitStoreWeb.Net9/frontend`
- Bundled output: `BitStoreWeb.Net9/wwwroot/dist`
- Installed UI libs: `bootstrap@5.3.8`, `jquery@4.0.0`

## Notes

- Static assets are served from `BitStoreWeb.Net9/wwwroot`.
