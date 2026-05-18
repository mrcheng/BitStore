# Deploy BitStore To Linux With MySQL

This guide deploys `BitStoreWeb.Net9` to a Linux host over SSH using:

- ASP.NET Core on `.NET 9`
- MySQL or MariaDB
- GitHub Actions
- SCP over SSH
- optional `systemd` restart

## Server Requirements

- SSH access
- `.NET 9` runtime installed
- MySQL or MariaDB database available
- A directory for the published app, for example `/httpdocs`
- A reverse proxy such as Nginx or Apache in front of Kestrel

## Production Settings

Configure these on the server, preferably in the `systemd` service environment or hosting control panel:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5017
ConnectionStrings__DefaultConnection=Server=YOUR_DB_HOST;Port=3306;Database=YOUR_DB;User=YOUR_USER;Password=YOUR_PASSWORD;
MySql__ServerVersion=8.4.0
Slack__RegistrationWebhookUrl=
```

Use the actual MySQL or MariaDB version from the host. For MariaDB, the app can still use the Pomelo MySQL EF provider, but `Program.cs` may need to be switched to `MariaDbServerVersion`.

## GitHub Secrets

Add these in GitHub under **Settings -> Secrets and variables -> Actions**:

```text
SSH_HOST=your-server-hostname
SSH_USER=your-ssh-user
SSH_PASSWORD=your-ssh-password
SSH_PORT=22
SSH_TARGET=/httpdocs
SSH_SERVICE_NAME=bitstore
```

`SSH_PORT`, `SSH_TARGET`, and `SSH_SERVICE_NAME` are optional. The workflow defaults to port `22` and target `/httpdocs`.

If `SSH_SERVICE_NAME` is set, the workflow runs:

```bash
sudo systemctl restart bitstore
```

The SSH user must be allowed to restart that service without an interactive password prompt.

## Workflow

The deploy workflow is:

```text
.github/workflows/deploy-linux-mysql.yml
```

It runs on pushes to `master`, and can also be started manually from GitHub Actions.

It performs:

1. Build the project on `windows-latest`.
2. Build the frontend on `ubuntu-latest`.
3. Publish the ASP.NET Core app on `ubuntu-latest`.
4. Upload `./publish` to the Linux server with `appleboy/scp-action`.
5. Restart the configured `systemd` service, if `SSH_SERVICE_NAME` is set.

## Example systemd Service

```ini
[Unit]
Description=BitStore
After=network.target

[Service]
WorkingDirectory=/httpdocs
ExecStart=/usr/bin/dotnet /httpdocs/BitStoreWeb.Net9.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=bitstore
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5017
Environment=ConnectionStrings__DefaultConnection=Server=YOUR_DB_HOST;Port=3306;Database=YOUR_DB;User=YOUR_USER;Password=YOUR_PASSWORD;
Environment=MySql__ServerVersion=8.4.0

[Install]
WantedBy=multi-user.target
```

## First Run

1. Deploy from GitHub Actions.
2. Open `/Account/Login?mode=register`.
3. Create the first account.
4. The first user becomes `SuperUser`.
5. Import old data from the admin users page if needed.
