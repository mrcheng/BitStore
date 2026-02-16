# Deploy BitStore To Azure (Step By Step)

This guide deploys `BitStoreWeb.Net9` using:

- Azure App Service (web hosting)
- Azure SQL Database (for production data)
- GitHub Actions (automatic deploys from `main`)

## 1) Prerequisites

- Azure subscription
- GitHub repo with this code
- `main` branch as your deploy branch

## 2) Create Azure SQL Database

1. In Azure Portal, create a new **SQL Database**.
2. On the database creation page, choose the **free offer** if it appears for your subscription.
3. The free offer includes monthly limits per database (100,000 vCore seconds, 32 GB data, 32 GB backup) and supports up to 10 free-offer databases per subscription.
4. Create or select a SQL server (save the server admin username/password).
5. After create completes, open the database and copy:
- Server name (example: `myserver.database.windows.net`)
- Database name
6. Keep the free-limit behavior as auto-pause to avoid surprise charges when monthly limits are reached.

## 3) Allow Azure Services To Reach SQL

1. Open your SQL server in Azure Portal.
2. Go to **Networking**.
3. Enable **Allow Azure services and resources to access this server**.
4. Save.

## 4) Create Azure App Service

1. Create a new **Web App** in Azure Portal.
2. Publish type: **Code**.
3. Runtime stack: **.NET 9**.
4. Operating system: Windows or Linux.
5. Choose a pricing tier that fits your budget.

Note: App Service Free/Shared tiers are intended for development/testing. For true production workloads, Standard tier or higher is recommended.

## 5) Configure App Service Settings

In App Service -> **Environment variables**, add:

- `DatabaseProvider` = `SqlServer`
- `ConnectionStrings__DefaultConnection` = your Azure SQL connection string

Example connection string:

```text
Server=tcp:YOUR_SERVER.database.windows.net,1433;Initial Catalog=YOUR_DB;Persist Security Info=False;User ID=YOUR_USER;Password=YOUR_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Save and restart the app.

## 6) Add GitHub Secrets

In GitHub -> repo -> **Settings** -> **Secrets and variables** -> **Actions**, add:

- `AZURE_WEBAPP_NAME`: your App Service name
- `AZURE_WEBAPP_PUBLISH_PROFILE`: publish profile XML from Azure

How to get publish profile:

1. Open App Service in Azure Portal.
2. Click **Get publish profile**.
3. Copy the full file contents into `AZURE_WEBAPP_PUBLISH_PROFILE`.

## 7) Deploy From GitHub

This repo includes two workflows:

- `.github/workflows/ci.yml`
- `.github/workflows/deploy-azure-webapp.yml`

Deploy happens automatically on push to `main`, or manually from **Actions** with `workflow_dispatch`.

## 8) First-Run Check

1. Open your production URL.
2. Go to `/Account/Login`.
3. Sign in with a new username/password to bootstrap the first super user.
4. Open `/Buckets` and create a bucket.
5. Verify API at `/api`.

## 9) Troubleshooting

- If deploy fails at Azure step: confirm both GitHub secrets are present and exact.
- If app starts but login fails: re-check SQL connection string and SQL firewall settings.
- If database errors mention permissions: verify the SQL user has rights on the target database.
