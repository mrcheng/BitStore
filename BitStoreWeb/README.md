# BitStore

Store a value in a bit.

BitStore is a lightweight bucket API + web app for teams that need to publish and read tiny values fast.
Instead of building ad-hoc key/value plumbing, you get a ready-to-run product with auth, UI, Swagger docs, and demo client out of the box.

## The Value

- Ship faster: create a bucket and start writing/reading in minutes.
- Integrate anywhere: use plain HTTP from apps, scripts, CI, cron jobs, and devices.
- Keep control simple: read by slug, write with `X-BitStore-Key`.
- Operate easily: manage buckets in UI, inspect API in Swagger, test in `/demo`.

## What You Get Right Now

- ASP.NET Core MVC app (`.NET 9`) at `BitStoreWeb.Net9`
- Bucket management UI (`/Buckets`)
- Tutorial page (`/HowTo`)
- Swagger API docs (`/api` -> `/swagger`)
- Logged-out demo client (`/demo`)
- SQLite persistence out of the box

## 2-Minute Quick Start

### 1) Run locally

```powershell
npm --prefix BitStoreWeb.Net9 install
npm --prefix BitStoreWeb.Net9 run build
dotnet run --project BitStoreWeb.Net9/BitStoreWeb.Net9.csproj
```

Open:

- App: `https://localhost:7001/`
- Buckets: `https://localhost:7001/Buckets`
- Tutorial: `https://localhost:7001/HowTo`
- API docs: `https://localhost:7001/api`
- Demo client: `https://localhost:7001/demo`

Note: API and Swagger now require login, so `/demo` requests in logged-out mode will be unauthorized unless you adjust that behavior.

### 2) Create your first bucket

1. Login: `https://localhost:7001/Account/Login`
2. Create a bucket in `/Buckets`
3. Copy `slug` (public identifier) and `write key` (secret token).

### 3) Read data

```bash
curl "https://localhost:7001/api/buckets/your-slug"
curl "https://localhost:7001/api/buckets/your-slug/latest"
```

### 4) Write data

```bash
curl -X POST "https://localhost:7001/api/buckets/your-slug/records" \
  -H "Content-Type: application/json" \
  -H "X-BitStore-Key: your-write-key" \
  -d "{\"value\":\"hello123\"}"
```

## API Snapshot

| Method | Route |
|---|---|
| GET | `/api/buckets/{slug}` |
| GET | `/api/buckets/{slug}/latest` |
| GET | `/api/buckets/{slug}/records?take=50` |
| POST | `/api/buckets/{slug}/records` |
| PUT | `/api/buckets/{slug}/records/{recordId}` |
| POST | `/api/buckets/{slug}/records/{recordId}/clear` |
| DELETE | `/api/buckets/{slug}/records/{recordId}` |
| DELETE | `/api/buckets/{slug}/records` |
| DELETE | `/api/buckets/{slug}` |

## Product Guardrails

- Max record value length: **8 characters**
- API and Swagger require authenticated session
- Reads require authenticated session and known slug
- Writes require valid key or owner session
- Use HTTPS in all environments
- Keep write keys out of shipped frontend code
- Prefer backend proxying for browser write flows

## Browser Integration Pattern (Recommended)

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

  // Keep write keys server-side in production.
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

`/your-backend/bitstore/write` is a placeholder route pattern. Create it in your own backend.

## Build

```powershell
npm --prefix BitStoreWeb.Net9 run build
dotnet build BitStoreWeb.Net9/BitStoreWeb.Net9.csproj
```

## Deploy

- Azure deployment walkthrough: `docs/DEPLOY_AZURE.md`
- GitHub Actions workflow: `.github/workflows/ci.yml`
- GitHub Actions workflow: `.github/workflows/deploy-azure-webapp.yml`

## Project Layout

- Main app: `BitStoreWeb.Net9`
- Controllers: `BitStoreWeb.Net9/Controllers`
- Views: `BitStoreWeb.Net9/Views`
- Frontend source: `BitStoreWeb.Net9/frontend`
- Bundled output: `BitStoreWeb.Net9/wwwroot/dist`
- Startup: `BitStoreWeb.Net9/Program.cs`

## Commercial Positioning

BitStore is for teams that want results without platform overhead.
If your product needs a simple, reliable place to share tiny values across systems, BitStore gives you a fast path from idea to production.
