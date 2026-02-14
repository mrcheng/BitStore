# BitStore

BitStore helps teams ship tiny but critical data flows fast.  
Create a bucket, publish short values, read them anywhere, and control writes with a key.

## Why Teams Choose BitStore

- Go live fast: bucket + API in minutes, not days.
- Simple integration: clean HTTP endpoints for apps, scripts, CI, and devices.
- Practical control: public reads by slug, protected writes by `X-BitStore-Key`.
- Built-in operator UX: modern bucket management UI, Swagger docs, and demo client.

## Product + Tutorial (Merged)

### 1. Run locally

```powershell
npm --prefix BitStoreWeb.Net9 install
npm --prefix BitStoreWeb.Net9 run build
dotnet run --project BitStoreWeb.Net9/BitStoreWeb.Net9.csproj
```

Open:

- App: `https://localhost:7001/`
- Buckets: `https://localhost:7001/Buckets`
- Tutorial page: `https://localhost:7001/HowTo`
- API docs: `https://localhost:7001/api` (redirects to `/swagger`)
- Demo client: `https://localhost:7001/demo`
- Demo client uses logged-out requests (`credentials: "omit"`), even if you are signed in.

### 2. Create your first bucket

1. Login at `https://localhost:7001/Account/Login`
2. Create a bucket in `/Buckets`
3. Copy:
   - `slug` (public identifier)
   - `write key` (secret token)

### 3. Read from anywhere

```bash
curl "https://localhost:7001/api/buckets/your-slug"
curl "https://localhost:7001/api/buckets/your-slug/latest"
```

### 4. Write with key protection

```bash
curl -X POST "https://localhost:7001/api/buckets/your-slug/records" \
  -H "Content-Type: application/json" \
  -H "X-BitStore-Key: your-write-key" \
  -d "{\"value\":\"hello123\"}"
```

## API Snapshot

- `GET /api/buckets/{slug}`
- `GET /api/buckets/{slug}/latest`
- `GET /api/buckets/{slug}/records?take=50`
- `POST /api/buckets/{slug}/records`
- `PUT /api/buckets/{slug}/records/{recordId}`
- `POST /api/buckets/{slug}/records/{recordId}/clear`
- `DELETE /api/buckets/{slug}/records/{recordId}`
- `DELETE /api/buckets/{slug}/records`
- `DELETE /api/buckets/{slug}`

## Limits And Security

- Max value length per record: **8 characters**
- Reads are public if slug is known
- Writes require valid key or owner session
- Use HTTPS in all environments
- Keep write keys out of shipped frontend code when possible
- Prefer backend proxying for browser write flows

## JavaScript Pattern (Safe + Clear Errors)

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

`/your-backend/bitstore/write` is a placeholder pattern. Create that backend endpoint in your own app.

## Build

```powershell
npm --prefix BitStoreWeb.Net9 run build
dotnet build BitStoreWeb.Net9/BitStoreWeb.Net9.csproj
```

## Project Layout

- Main app: `BitStoreWeb.Net9`
- Controllers: `BitStoreWeb.Net9/Controllers`
- Views: `BitStoreWeb.Net9/Views`
- Frontend source: `BitStoreWeb.Net9/frontend`
- Bundled output: `BitStoreWeb.Net9/wwwroot/dist`
- Startup: `BitStoreWeb.Net9/Program.cs`
