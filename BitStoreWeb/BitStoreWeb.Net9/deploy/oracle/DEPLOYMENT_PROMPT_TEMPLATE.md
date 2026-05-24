# Oracle VM Docker Deployment Prompt Template

Use this as a prompt/context file when deploying another ASP.NET Core site to the same Oracle VM pattern.

## Goal

Deploy an ASP.NET Core/.NET app to an Oracle Cloud Ubuntu VM using Docker, Caddy, and a shared MariaDB container.

Target architecture:

```text
Internet -> 80/443 -> Caddy -> app container internal port
                         |
                         -> shared Docker network -> MariaDB
```

For BitStore specifically:

```text
https://bitstore.mrcheng.se/
Internet -> 80/443 -> Caddy -> bitstore-web:8080
MariaDB container -> bitstore database / bitstore_app user
```

## Oracle VM

```text
Public IP: 82.70.47.203
SSH user: ubuntu
SSH key on Windows: C:\Users\danie\.ssh\oracle.key
Architecture: ARM/aarch64
OS: Ubuntu 24.04 LTS
Sudo: ubuntu user can use sudo
```

SSH command:

```powershell
ssh -i C:\Users\danie\.ssh\oracle.key ubuntu@82.70.47.203
```

Check OS/arch:

```bash
uname -a
lsb_release -a
```

## Oracle Cloud Networking

Open these inbound ports in the Oracle VCN Security List or Network Security Group attached to the VM:

```text
TCP 22   from your IP or 0.0.0.0/0 for SSH
TCP 80   from 0.0.0.0/0 for Caddy/Let's Encrypt HTTP challenge
TCP 443  from 0.0.0.0/0 for HTTPS
```

Do not require public app ports like `8080` in the final setup.

The VM firewall also needs to allow 80/443:

```bash
sudo iptables -C INPUT -p tcp --dport 80 -j ACCEPT 2>/dev/null || sudo iptables -I INPUT 5 -p tcp --dport 80 -j ACCEPT
sudo iptables -C INPUT -p tcp --dport 443 -j ACCEPT 2>/dev/null || sudo iptables -I INPUT 6 -p tcp --dport 443 -j ACCEPT
sudo apt-get install -y iptables-persistent
sudo sh -c 'iptables-save > /etc/iptables/rules.v4'
```

Remove any temporary public app-port rule when Caddy is working:

```bash
while sudo iptables -C INPUT -p tcp --dport 8080 -j ACCEPT 2>/dev/null; do
  sudo iptables -D INPUT -p tcp --dport 8080 -j ACCEPT
done
sudo sh -c 'iptables-save > /etc/iptables/rules.v4'
```

## Docker Install

On the VM:

```bash
sudo apt-get update
sudo apt-get install -y docker.io docker-compose-v2
sudo systemctl enable --now docker
sudo usermod -aG docker ubuntu
```

We still used `sudo docker ...` in scripts so a fresh login is not required.

## VM Directory Layout

```text
/opt/stacks/shared      shared services such as MariaDB
/opt/stacks/proxy       Caddy reverse proxy
/opt/stacks/bitstore    BitStore app stack
/opt/stacks/bitstore/src uploaded BitStore source used for docker build
```

Create base directories:

```bash
sudo mkdir -p /opt/stacks/shared /opt/stacks/proxy /opt/stacks/bitstore/src
sudo chown -R ubuntu:ubuntu /opt/stacks
```

## Shared MariaDB

Use one shared MariaDB container for multiple projects, with separate DBs/users per project.

Example structure:

```text
mariadb container
├── bitstore database / bitstore_app user
├── project2 database / project2_app user
└── project3 database / project3_app user
```

Compose template:

```yaml
services:
  mariadb:
    image: mariadb:11.4
    container_name: mariadb
    restart: unless-stopped
    environment:
      MARIADB_ROOT_PASSWORD: ${MARIADB_ROOT_PASSWORD}
    volumes:
      - mariadb_data:/var/lib/mysql
    networks:
      - shared_backend
    healthcheck:
      test: ["CMD-SHELL", "mariadb-admin ping -h localhost -uroot -p$${MARIADB_ROOT_PASSWORD} --silent"]
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s

volumes:
  mariadb_data:
    name: mariadb_data

networks:
  shared_backend:
    name: shared_backend
```

Save as:

```text
/opt/stacks/shared/docker-compose.yml
```

Create `/opt/stacks/shared/.env`:

```text
MARIADB_ROOT_PASSWORD=<strong generated password>
```

Start MariaDB:

```bash
cd /opt/stacks/shared
sudo docker compose up -d
sudo docker ps
```

Create database/user for a project:

```bash
set -a
. /opt/stacks/shared/.env
. /opt/stacks/bitstore/.env
set +a

sudo docker exec -i mariadb mariadb -uroot -p"$MARIADB_ROOT_PASSWORD" <<SQL
CREATE DATABASE IF NOT EXISTS \`$BITSTORE_DB_NAME\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS '$BITSTORE_DB_USER'@'%' IDENTIFIED BY '$BITSTORE_DB_PASSWORD';
ALTER USER '$BITSTORE_DB_USER'@'%' IDENTIFIED BY '$BITSTORE_DB_PASSWORD';
GRANT ALL PRIVILEGES ON \`$BITSTORE_DB_NAME\`.* TO '$BITSTORE_DB_USER'@'%';
FLUSH PRIVILEGES;
SQL
```

MariaDB should not be published publicly. Other app containers connect over `shared_backend` using host `mariadb`.

## ASP.NET Core Dockerfile

For BitStore, the Dockerfile is in:

```text
BitStoreWeb/BitStoreWeb.Net9/Dockerfile
```

Pattern:

```dockerfile
FROM node:22-bookworm-slim AS frontend
WORKDIR /src
COPY package*.json ./
RUN npm ci
COPY frontend ./frontend
COPY vite.config.js ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY BitStoreWeb.Net9.csproj ./
RUN dotnet restore BitStoreWeb.Net9.csproj
COPY . ./
COPY --from=frontend /src/wwwroot/dist ./wwwroot/dist
RUN dotnet publish BitStoreWeb.Net9.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "BitStoreWeb.Net9.dll"]
```

Use `.dockerignore`:

```text
bin/
bin_verify/
obj/
node_modules/
wwwroot/dist/
bitstore.db
bitstore.db-shm
bitstore.db-wal
*.log
.git/
.vs/
```

For ARM/aarch64 Oracle VMs, normal multi-arch Docker images from Microsoft/Node/Caddy/MariaDB work.

## App Stack

BitStore compose:

```yaml
services:
  bitstore-web:
    build:
      context: ./src
      dockerfile: Dockerfile
    container_name: bitstore-web
    restart: unless-stopped
    env_file:
      - .env
    expose:
      - "8080"
    volumes:
      - bitstore_data_protection:/root/.aspnet/DataProtection-Keys
    networks:
      - shared_backend

volumes:
  bitstore_data_protection:
    name: bitstore_data_protection

networks:
  shared_backend:
    external: true
    name: shared_backend
```

Save as:

```text
/opt/stacks/bitstore/docker-compose.yml
```

BitStore `.env`:

```text
BITSTORE_DB_NAME=bitstore
BITSTORE_DB_USER=bitstore_app
BITSTORE_DB_PASSWORD=<strong generated password>
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__DefaultConnection=Server=mariadb;Port=3306;Database=bitstore;User=bitstore_app;Password=<same strong generated password>;
MySql__ServerType=MariaDb
MySql__ServerVersion=11.4.0
Slack__RegistrationWebhookUrl=
```

Important:

- Do not commit `.env`.
- Keep data-protection keys in a persistent volume so logins/cookies survive container recreation.
- Keep `8080` internal; Caddy reaches it over Docker networking.

Start/rebuild app:

```bash
cd /opt/stacks/bitstore
sudo docker compose up -d --build bitstore-web
sudo docker compose ps
sudo docker logs --tail 100 bitstore-web
```

## Caddy Reverse Proxy

Use Caddy so HTTPS is automatic through Let's Encrypt.

For BitStore:

```text
bitstore.mrcheng.se {
    encode zstd gzip
    reverse_proxy bitstore-web:8080
}
```

Save as:

```text
/opt/stacks/proxy/Caddyfile
```

Caddy compose:

```yaml
services:
  caddy:
    image: caddy:2-alpine
    container_name: caddy
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
      - "443:443/udp"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config
    networks:
      - shared_backend

volumes:
  caddy_data:
    name: caddy_data
  caddy_config:
    name: caddy_config

networks:
  shared_backend:
    external: true
    name: shared_backend
```

Save as:

```text
/opt/stacks/proxy/docker-compose.yml
```

Start Caddy:

```bash
cd /opt/stacks/proxy
sudo docker compose up -d
sudo docker logs --tail 120 caddy
```

Expected Caddy success log:

```text
certificate obtained successfully
identifier: bitstore.mrcheng.se
issuer: acme-v02.api.letsencrypt.org-directory
```

If Caddy says challenge timeout, check:

- DNS A record points to VM IP.
- Oracle VCN/NSG allows inbound 80 and 443.
- VM iptables allows inbound 80 and 443.
- No other process is using 80/443.

## DNS

For BitStore:

```text
bitstore.mrcheng.se A 82.70.47.203
```

Check from Windows:

```powershell
Resolve-DnsName bitstore.mrcheng.se
```

Check HTTP/HTTPS:

```powershell
curl.exe -v http://bitstore.mrcheng.se/
curl.exe -v https://bitstore.mrcheng.se/
```

HTTP should redirect to HTTPS. HTTPS should return the app.

## Deploy Script

BitStore has a repeatable deploy script:

```text
BitStoreWeb/BitStoreWeb.Net9/deploy/oracle/deploy-oracle.ps1
```

Run from `BitStoreWeb.Net9`:

```powershell
.\deploy\oracle\deploy-oracle.ps1
```

It:

1. Runs `dotnet build` locally into `bin_verify`.
2. Packages source while excluding build output, local DB files, logs, `.env`, and generated frontend dist.
3. Uploads a tar archive to the VM.
4. Extracts it to `/opt/stacks/bitstore/src`.
5. Rebuilds/restarts `bitstore-web`.
6. Verifies the public HTTPS URL.

Useful flags:

```powershell
.\deploy\oracle\deploy-oracle.ps1 -SkipBuildCheck
.\deploy\oracle\deploy-oracle.ps1 -UploadCompose
```

The script parameters:

```powershell
-HostName "82.70.47.203"
-UserName "ubuntu"
-KeyPath "$env:USERPROFILE\.ssh\oracle.key"
-RemoteAppDir "/opt/stacks/bitstore"
-PublicUrl "https://bitstore.mrcheng.se/"
```

## GitHub Actions Auto Deploy

Workflow:

```text
.github/workflows/deploy-oracle.yml
```

It runs on push to `master` or `main` when files under `BitStoreWeb/BitStoreWeb.Net9/**` change, and can also be run manually.

GitHub secret:

```text
ORACLE_SSH_PRIVATE_KEY = contents of C:\Users\danie\.ssh\oracle.key
```

GitHub variables:

```text
ORACLE_HOST=82.70.47.203
ORACLE_SSH_USER=ubuntu
ORACLE_REMOTE_APP_DIR=/opt/stacks/bitstore
BITSTORE_PUBLIC_URL=https://bitstore.mrcheng.se/
```

The workflow:

1. Checks out repo.
2. Installs .NET 9.
3. Writes SSH key to `~/.ssh/oracle.key`.
4. Adds VM host key with `ssh-keyscan`.
5. Calls `deploy/oracle/deploy-oracle.ps1`.

## Common Checks

List containers:

```bash
sudo docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
```

Expected BitStore setup:

```text
caddy          public 80/443
bitstore-web   internal 8080 only
mariadb        internal 3306 only, healthy
```

Check logs:

```bash
sudo docker logs --tail 100 bitstore-web
sudo docker logs --tail 100 caddy
sudo docker logs --tail 100 mariadb
```

Check local app from VM:

```bash
curl -i http://bitstore-web:8080/
curl -i -H 'Host: bitstore.mrcheng.se' http://127.0.0.1/
```

Check public site:

```bash
curl -I https://bitstore.mrcheng.se/
```

## Notes For Another Site

For a new project on the same VM:

1. Reuse the existing `mariadb`, `caddy`, and `shared_backend`.
2. Create a separate database and user in MariaDB.
3. Create a new app stack folder, e.g. `/opt/stacks/project-name`.
4. Give the app its own `.env`.
5. Add a Caddy block:

```text
project.example.com {
    encode zstd gzip
    reverse_proxy project-container:8080
}
```

6. Put the new app container on `shared_backend`.
7. Keep app ports internal with `expose`, not public `ports`.
8. Add a project-specific deploy script or parameterize the BitStore one.
