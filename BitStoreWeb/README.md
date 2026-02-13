# BitStore

This repository now hosts the modernized ASP.NET Core MVC app at:

- `BitStoreWeb.Net9` (`.NET 9`)

The legacy `.NET Framework 4.5` / FubuMVC project has been removed.

## Run

```powershell
cd BitStoreWeb.Net9
dotnet run
```

## Build

```powershell
dotnet build BitStoreWeb.Net9/BitStoreWeb.Net9.csproj
```

## Notes

- Current pages are still mostly UI/mock content (home, buckets, bucket, how-to, API).
- Static assets are served from `BitStoreWeb.Net9/wwwroot`.
