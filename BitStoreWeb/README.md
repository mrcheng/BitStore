# BitStore

Store tiny values anywhere. Read them instantly from anywhere.

BitStore is a lightweight bucket API + web UI for cross-system value sharing.  
It is built for fast shipping: create a bucket, get a slug + write key, and start reading/writing in minutes.

## Why BitStore

- Fast setup: go from zero to live bucket in under 5 minutes.
- API-first: clean HTTP endpoints for scripts, apps, CI, and devices.
- Practical security model: public reads by slug, protected writes by key.
- Operator-friendly: create and manage buckets in a modern MVC UI.
- Demo-ready: includes Swagger docs and a logged-out demo client page.

## Ideal Use Cases

- Feature flags and tiny runtime switches
- Build/deploy status markers
- Cross-service "latest value" sharing
- Device/app heartbeat or simple telemetry pointers
- Public read + controlled write scenarios

## Current Product Scope

- App project: `BitStoreWeb.Net9` (`ASP.NET Core MVC`, `.NET 9`)
- Bucket read endpoints by slug
- Write/update/delete endpoints protected by `X-BitStore-Key`
- Bucket management UI (`/Buckets`)
- API docs via Swagger (`/api` redirects to `/swagger`)
- Demo client (`/demo`)

## Data Limits

- Record value max length: **8 characters**
- Reads are public by slug
- Writes require valid key or owner session

## Quick Start (Local)

```powershell
npm --prefix BitStoreWeb.Net9 install
npm --prefix BitStoreWeb.Net9 run build
dotnet run --project BitStoreWeb.Net9/BitStoreWeb.Net9.csproj
```

Then open:

- App: `https://localhost:7001/`
- Buckets UI: `https://localhost:7001/Buckets`
- Tutorial page: `https://localhost:7001/HowTo`
- Swagger: `https://localhost:7001/api`
- Demo client: `https://localhost:7001/demo`

## First 5 Minutes Tutorial

1. Open `https://localhost:7001/Account/Login` and sign in.
2. Go to `Buckets`, create a new bucket.
3. Open that bucket and copy:
   - `Slug` (public identifier)
   - `Write key` (secret token)
4. Test read:

```bash
curl "https://localhost:7001/api/buckets/your-slug"
```

5. Test write:

```bash
curl -X POST "https://localhost:7001/api/buckets/your-slug/records" ^
  -H "Content-Type: application/json" ^
  -H "X-BitStore-Key: your-write-key" ^
  -d "{\"value\":\"hello123\"}"
```

6. Confirm latest:

```bash
curl "https://localhost:7001/api/buckets/your-slug/latest"
```

## JavaScript Example (Status + Validation)

```javascript
const MAX_VALUE_LENGTH = 8;
const base = "https://localhost:7001/api/buckets/your-slug";

function apiErrorMessage(payload, fallback) {
  if (payload?.message) return payload.message;
  if (payload?.errors) {
    const all = Object.values(payload.errors).flat();
    if (all.length) return all[0];
  }
  return fallback;
}

async function fetchJson(url, options) {
  const response = await fetch(url, options);
  const text = await response.text();
  const data = text ? JSON.parse(text) : {};
  if (!response.ok) {
    throw new Error(apiErrorMessage(data, `HTTP ${response.status} ${response.statusText}`));
  }
  return data;
}

async function addValue(value) {
  if (value.length > MAX_VALUE_LENGTH) {
    throw new Error(`Value must be ${MAX_VALUE_LENGTH} characters or fewer.`);
  }

  // Production best practice: keep write keys server-side (do not ship keys in browser code).
  return fetchJson("/your-backend/bitstore/write", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ bucketSlug: "your-slug", value })
  });
}

await addValue("1337");
const latestPayload = await fetchJson(`${base}/latest`);
console.log(latestPayload.record);
```

## API Quick Reference

- `GET /api/buckets/{slug}`
- `GET /api/buckets/{slug}/latest`
- `GET /api/buckets/{slug}/records?take=50`
- `POST /api/buckets/{slug}/records`
- `PUT /api/buckets/{slug}/records/{recordId}`
- `POST /api/buckets/{slug}/records/{recordId}/clear`
- `DELETE /api/buckets/{slug}/records/{recordId}`
- `DELETE /api/buckets/{slug}/records`
- `DELETE /api/buckets/{slug}`

## Security Notes

- Reads are public by design if slug is known.
- Treat write keys like secrets.
- Use HTTPS in all environments.
- Do not commit write keys to source control.
- Prefer backend proxying for browser-based writes.

## Build

```powershell
npm --prefix BitStoreWeb.Net9 run build
dotnet build BitStoreWeb.Net9/BitStoreWeb.Net9.csproj
```

## Project Layout

- Controllers: `BitStoreWeb.Net9/Controllers`
- Views: `BitStoreWeb.Net9/Views`
- Frontend source: `BitStoreWeb.Net9/frontend`
- Built assets: `BitStoreWeb.Net9/wwwroot/dist`
- Startup: `BitStoreWeb.Net9/Program.cs`

## Commercial Pitch

BitStore gives teams a low-friction way to ship "small but critical" data flows without standing up heavy infrastructure.  
If your system needs a reliable place to publish and fetch tiny values quickly, BitStore is the practical path from idea to production.
