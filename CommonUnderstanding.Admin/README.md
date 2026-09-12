# Common Understanding Admin

Local-only operations dashboard for production uptake, availability, App Service traffic, and Azure SQL pressure.

## Setup

1. Sign in with the Azure CLI account that can read the `CommonUnderstanding` resource group:

   ```powershell
   az login
   az account set --subscription 3425f381-90e9-49d2-8a85-4c3cab0599ab
   ```

2. Add a read-only production database connection string to local user secrets:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:ReadOnlyDatabase" "<connection-string>" --project .\CommonUnderstanding.Admin
   ```

   Use a database principal restricted to `db_datareader`. No secret is committed to Git. Adoption metrics remain unavailable, without breaking the dashboard, when this setting is absent.

3. Run the dashboard:

   ```powershell
   dotnet run --project .\CommonUnderstanding.Admin
   ```

   Open `http://localhost:5198`.

## Metric definitions

- **Total users:** non-service rows in `UserAccounts`.
- **Active now:** distinct non-service users with discovery or XP activity in the last 15 minutes. This is an approximation because the application does not persist every login or global SignalR connection.
- **New users, arguments, and votes:** production records created within the selected range.
- **AI requests:** request counters whose most recent request falls within the selected range. This is directional, not a precise interval delta.
- **App and database pressure:** Azure Monitor platform metrics, fetched using `DefaultAzureCredential` and cached for five minutes.
- **Observed uptime:** successful checks made while this local process is running. It is not historical SLA data.

The dashboard never polls automatically. Production database results are cached for 15 minutes so normal dashboard use does not continuously hold the serverless database awake.
