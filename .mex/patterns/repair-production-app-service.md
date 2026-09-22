---
name: repair-production-app-service
description: Diagnose and repair a live Common Understanding App Service failure using production evidence, focused validation, clean deployment, and post-restart proof.
triggers:
  - "production is broken"
  - "endpoint returns 500"
  - "recommended feed is down"
  - "App Service incident"
  - "production regression"
edges:
  - target: patterns/deploy-azure.md
    condition: when the diagnosis is complete and a package must be deployed
  - target: patterns/change-database-schema.md
    condition: only when production evidence proves schema drift or a migration is required
  - target: patterns/debug-background-processing.md
    condition: when the failing path is owned by a queue or hosted worker
grounds_to:
  - CommonUnderstanding/Program.cs
  - CommonUnderstanding/CommonUnderstanding.csproj
last_updated: 2026-09-20
---

# Repair a Production App Service Failure

## Fixed Target

```powershell
$subscription = '3425f381-90e9-49d2-8a85-4c3cab0599ab'
$resourceGroup = 'CommonUnderstanding'
$app = 'common-understanding-v2'
$baseUrl = 'https://common-understanding-v2.azurewebsites.net'
$project = '.\CommonUnderstanding\CommonUnderstanding.csproj'
$publish = '.\CommonUnderstanding\bin\Release\net9.0\publish'
```

Production uses Linux App Service, .NET 9, SQL Server, and database `CommonUnderstanding`. `Program.cs` runs `MigrateAsync()` at startup, so migration state must be known before any restart.

## Fast Path

Follow this order. Do not inspect migrations, broad code surfaces, or deployment scripts before obtaining the production exception.

1. Reproduce the failing route and one nearby control route.
2. Download current logs and extract the exact exception and owning source line.
3. Read only that owning method and its nearest focused test.
4. State one cause and one check that can disprove it.
5. Make the smallest repair, then immediately run the focused test.
6. Build. Check migration state only because deployment restarts the app.
7. Clean, publish, inspect, package, and deploy once.
8. Prove recovery with route responses and a post-restart log window.

Stop as soon as a step identifies the controlling fault. Do not continue exploring competing theories unless the next check disproves the current one.

## 1. Establish the Failure Boundary

Use non-interactive HTTP requests. On Windows PowerShell 5.1, include `-UseBasicParsing` to avoid the HTML parsing prompt.

```powershell
$routes = @('/', '/api/feed?offset=0&limit=5', '/api/feed/recommended?offset=0&limit=5')
foreach ($route in $routes) {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri ($baseUrl + $route) -TimeoutSec 180
        [pscustomobject]@{ Route = $route; Status = [int]$response.StatusCode; Bytes = $response.RawContentLength }
    } catch {
        [pscustomobject]@{ Route = $route; Status = [int]$_.Exception.Response.StatusCode; Error = $_.Exception.Message }
    }
}
```

A working control route plus one failing route localizes the incident. Do not infer schema drift from a generic 500 page.

## 2. Read the Real Exception First

The configured Azure CLI extension directory is damaged on this workstation. Set an empty temporary extension directory before every `az` operation.

```powershell
$extensionDir = Join-Path $env:TEMP 'az-empty-extensions'
New-Item -ItemType Directory -Path $extensionDir -Force | Out-Null
$env:AZURE_EXTENSION_DIR = $extensionDir
az account set --subscription $subscription

$logZip = Join-Path $env:TEMP "$app-logs.zip"
Remove-Item $logZip -Force -ErrorAction SilentlyContinue
az webapp log download --name $app --resource-group $resourceGroup --log-file $logZip
```

Kudu archives can contain colon-bearing filenames that Windows cannot extract. Do not use `Expand-Archive`. List and stream entries directly:

```powershell
$entries = @(tar.exe -tf $logZip)
$entries | Where-Object { $_ -match 'default_docker\.log$|containerStream\.log$' } | Select-Object -Last 20

$entry = $entries | Where-Object { $_ -match 'default_docker\.log$' } | Select-Object -Last 1
$lines = @(tar.exe -xOf $logZip $entry)
$lines | Select-String -Pattern 'Unhandled exception|ReadOnlySpan|SqlException|Invalid column|fail:' -CaseSensitive:$false | Select-Object -Last 80
```

Use the exception’s first application frame as the code anchor. If it names an EF query, inspect that expression before examining migrations.

## 3. Known .NET 9 / EF Failure

Symptom:

```text
ReadOnlySpan<Guid>
```

Cause: a captured `Guid[]` used by `Contains` inside an EF expression can bind through a .NET 9 span path that EF's parameter funcletizer cannot interpret. The failure occurs before SQL is sent.

Repair all equivalent EF-bound ID collections in the owning method:

```csharp
var ids = source.Select(item => item.Id).ToList();
query = query.Where(row => ids.Contains(row.Id));
```

Do not change unrelated arrays. A successful SQL Server execution emits `OPENJSON(@__...Ids...)`, which is useful post-deployment proof.

## 4. Focused Validation

Run the narrow test immediately after the first edit:

```text
CommonUnderstanding.Tests/Services/FeedRankingServiceTests.cs
```

Then run the workspace `build` task. Do not deploy when the focused tests or build fail. Existing tests may cover ranking math without executing EF translation; production logs and post-deployment SQL markers remain required evidence for this incident class.

## 5. Check Restart Migration Risk

Skip this section when no restart or deployment will occur. Otherwise, compare repository migrations to production because startup always calls `MigrateAsync()`.

Create a temporary single-IP SQL firewall rule, query with EF, and remove the rule in `finally`. Never print the connection string.

```powershell
$sqlServer = 'cu-sql-zhpcjg'
$ruleName = 'copilot-migration-audit'
$publicIp = (Invoke-RestMethod -Uri 'https://api.ipify.org').Trim()
try {
    az sql server firewall-rule create --resource-group $resourceGroup --server $sqlServer `
        --name $ruleName --start-ip-address $publicIp --end-ip-address $publicIp --output none

    $settings = az webapp config appsettings list --name $app --resource-group $resourceGroup | ConvertFrom-Json
    $env:ConnectionStrings__DefaultConnection = ($settings | Where-Object name -eq 'ConnectionStrings__DefaultConnection' | Select-Object -First 1).value
    $env:DatabaseProvider = 'SqlServer'
    dotnet ef migrations list --project $project --startup-project $project --no-build `
        --configuration Debug --connection $env:ConnectionStrings__DefaultConnection
    if ($LASTEXITCODE -ne 0) { throw 'Migration history query failed.' }
} finally {
    Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
    az sql server firewall-rule delete --resource-group $resourceGroup --server $sqlServer `
      --name $ruleName --output none 2>$null
}
```

Verify deletion with `az sql server firewall-rule list`. If any migration is marked `(Pending)`, stop: review it under `change-database-schema.md` before restarting production. If none are pending, continue.

## 6. Build a Clean Package

`dotnet publish` does not clean stale output. Delete only the generated publish directory first.

```powershell
Remove-Item $publish -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish $project -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }

$forbidden = @(Get-ChildItem $publish -Directory -Recurse | Where-Object { $_.Name -in @('artifacts', '.git', 'publish') })
if ($forbidden.Count -ne 0) { throw "Forbidden publish directories: $($forbidden.FullName -join ', ')" }
if (-not (Test-Path (Join-Path $publish 'CommonUnderstanding.dll'))) { throw 'Application DLL is missing.' }
```

Package with `tar.exe` so ZIP entries use Linux-compatible separators:

```powershell
$package = Join-Path $env:TEMP "$app-repair.zip"
Remove-Item $package -Force -ErrorAction SilentlyContinue
tar.exe -a -c -f $package -C (Resolve-Path $publish).Path .
$packageEntries = @(tar.exe -tf $package)
if ($packageEntries -match '\\') { throw 'ZIP contains backslash entries.' }
if ($packageEntries -match '(^|/)artifacts(/|$)') { throw 'ZIP contains generated artifacts.' }
if (-not ($packageEntries -match '(^|/)CommonUnderstanding\.dll$')) { throw 'ZIP is missing the application DLL.' }
```

## 7. Deploy Once

```powershell
az webapp deploy --resource-group $resourceGroup --name $app --src-path $package `
    --type zip --clean true --restart true --async false --output json
```

Do not issue a second deployment while OneDeploy is still running. This B1 app can take more than two minutes to cold start.

## 8. Prove Recovery

Repeat the exact failing route and its control route. Require expected status, content type, and non-empty data where applicable.

Download a fresh log archive, stream the current container log with `tar.exe -xOf`, and restrict analysis to the actual post-restart timestamp. Require:

- zero matches for the original exception;
- zero `Unhandled exception`, `SqlException`, or `fail:` entries;
- expected SQL markers when relevant, such as `OPENJSON(@__candidateIds_0)`;
- App Service state `Running`.

Do not scan the entire retained archive without a timestamp boundary; historical errors will produce false failures.

## Decision Rules

- Exact application stack frame found: inspect and test that method; stop broad investigation.
- `ReadOnlySpan<Guid>` before SQL: replace EF-captured `Guid[]` with `List<Guid>` at equivalent query sites.
- `Invalid column` or `Invalid object`: inspect migration history and model/migration ownership.
- SQL availability/login error: treat as database connectivity or credential failure, not a package defect.
- Package upload succeeds but site fails: inspect startup logs before redeploying.
- Smoke route succeeds and the bounded post-restart log window is clean: incident is closed.

## GROW

- Update `.mex/ROUTER.md` only with durable production state.
- Add newly discovered repeatable pitfalls to this runbook.
- Run `mex log --type decision "<cause, repair, and production proof>"` when the rationale matters.