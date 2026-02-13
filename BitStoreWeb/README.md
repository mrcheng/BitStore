# BitStoreWeb

BitStoreWeb is a legacy ASP.NET Web Application (`.NET Framework 4.5`) built with `FubuMVC`, `Razor`, and `StructureMap`.

This codebase currently looks like a product/demo website for "BitStore" rather than a fully implemented storage backend. Most endpoint actions return empty view models, and most visible behavior is in static Razor markup and client-side scripts.

## What It Contains

- Home/marketing page (`Home.cshtml`)
- Buckets listing and bucket detail UI mockups (`Endpoints/Buckets`, `Endpoints/Bucket`)
- "How to use" walkthrough (`Endpoints/HowTo`)
- API reference placeholder page (`Endpoints/Examples/API.cshtml`)
- Shared layout and navigation (`Shared/Application.cshtml`)

## Tech Stack

- `C#` + ASP.NET Web Application project format
- `TargetFrameworkVersion`: `v4.5`
- `FubuMVC` routing and endpoint conventions
- `Razor` views (`.cshtml`)
- `StructureMap` for IoC bootstrapping
- Frontend libraries: Bootstrap 2.x, jQuery 1.7/1.9, jQuery UI 1.8, Knockout 2.1 (mostly static/demo usage)

## Project Structure

- `App_Start/FubuMVC.cs`: startup bootstrap (`WebActivator` + `FubuApplication`)
- `ConfigureFubuMVC.cs`: root Fubu registry setup
- `Endpoints/*`: endpoint classes, input models, view models, and views by feature
- `Shared/Application.cshtml`: layout shell and navbar
- `Content/`, `Scripts/`, `Images/`: static assets
- `packages.config` + `packages/`: legacy NuGet package management

## Routing Notes

Routes are convention-based through FubuMVC endpoint methods (for example `get_howto`, `get_buckets`, `get_bucket`) plus an explicit home route:

- `Routes.HomeIs<HomeInputModel>()` in `ConfigureFubuMVC.cs`

In practice, expect pages corresponding to:

- `/` (home)
- `/howto`
- `/buckets`
- `/bucket`
- `/api` (or `/API`, depending on convention handling)

## Local Setup

### Prerequisites

- Windows
- Visual Studio with support for ASP.NET (`.NET Framework 4.5`)
- `MSBuild` available in your developer shell (or via a Visual Studio Developer Command Prompt)

### Build

```powershell
msbuild BitStoreWeb.sln /t:Build /p:Configuration=Debug
```

If packages are missing:

```powershell
nuget restore BitStoreWeb.sln
```

### Run

1. Open `BitStoreWeb.sln` in Visual Studio.
2. Run with IIS Express (project is configured for IIS Express in `BitStoreWeb.csproj`).
3. Open the local URL shown by Visual Studio.

## Current Limitations

- No implemented persistence/service layer in this project.
- Several pages use placeholder data/text (for example test bucket rows and lorem ipsum API docs).
- No automated tests are included.
- Legacy dependencies and framework versions should be treated as compatibility-sensitive.
