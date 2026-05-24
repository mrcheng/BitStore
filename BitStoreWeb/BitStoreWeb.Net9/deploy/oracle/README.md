# Oracle VM Deployment

This folder documents the Docker deployment currently used on the Oracle Cloud VM.

## Layout On The VM

```text
/opt/stacks/shared      shared MariaDB container
/opt/stacks/proxy       Caddy reverse proxy
/opt/stacks/bitstore    BitStore app container and uploaded source
```

## Normal App Deploy

From `BitStoreWeb.Net9` on Windows:

```powershell
.\deploy\oracle\deploy-oracle.ps1
```

The script:

1. Runs a local `dotnet build` into `bin_verify`.
2. Packages the source without `bin`, `obj`, `node_modules`, local DB files, logs, or `.env` files.
3. Uploads the package to the VM.
4. Rebuilds and restarts only `bitstore-web`.
5. Verifies `https://bitstore.mrcheng.se/`.

Use `-SkipBuildCheck` only when you intentionally want Docker on the VM to be the first build check.

Use `-UploadCompose` if the BitStore app compose template changed and should overwrite `/opt/stacks/bitstore/docker-compose.yml`.

## Reusable Infrastructure Templates

The compose files in `shared`, `proxy`, and `bitstore` mirror the VM setup. They are templates, not secret stores.

Secrets live only in VM `.env` files:

```text
/opt/stacks/shared/.env
/opt/stacks/bitstore/.env
```

Do not commit real `.env` files.

## Public Access

Caddy serves:

```text
https://bitstore.mrcheng.se/
```

BitStore stays internal on Docker port `8080`. MariaDB stays internal on Docker port `3306`.
