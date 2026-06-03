# Bug report API (Render)

PES3-Disc submits bug reports to a small ASP.NET API. The API source and Render deploy config live in **this repo** so GitHub/Render can build them.

| File | Purpose |
|------|---------|
| [`render.yaml`](../render.yaml) | Render Blueprint — web service config |
| [`Dockerfile`](../Dockerfile) | Builds `services/PES3.BugReports.Api` |
| [`services/PES3.BugReports.Api/`](PES3.BugReports.Api/) | REST API (SQLite, clustering, rate limit) |

The **PES3 Dev Client** (local WPF app for reading grouped reports) is maintained separately in the `PES3-Dev` folder on your machine; only the API is deployed from this GitHub repo.

## Deploy to Render

1. [Render](https://render.com) → **New → Blueprint**
2. Connect **`SlushFlow/PES3-Disc`** (this repo)
3. Render reads `render.yaml` from the repo root
4. Set **`DEV_API_KEY`** in the service Environment tab (long random secret)
5. After deploy, verify: `curl https://YOUR-SERVICE.onrender.com/health`

Default URL baked into PES3-Disc: `https://pes3-bugreports.onrender.com` — update [`BugReportEndpoints.cs`](../src/PES3-Disc.BugReports/BugReportEndpoints.cs) if your Render URL differs.

### Free vs paid storage

The blueprint uses **`plan: free`**. Render **does not allow persistent disks on the free tier**, so SQLite is stored at `/tmp/reports.db`. Reports work, but data is **lost** when the service redeploys, restarts, or spins down after idle.

For durable bug reports, upgrade the service to **Starter** (or higher) in the Render dashboard, then add a disk:

```yaml
    plan: starter
    envVars:
      - key: DATABASE_PATH
        value: /data/reports.db
    disk:
      name: pes3-bugreports-data
      mountPath: /data
      sizeGB: 1
```

Remove the free-tier `/tmp` `DATABASE_PATH` if you switch to a disk.

## Endpoints

| Method | Path | Auth |
|--------|------|------|
| GET | `/health` | none |
| POST | `/api/reports` | none (rate-limited) |
| GET | `/api/reports` | `X-Dev-Key` |
| GET | `/api/summaries` | `X-Dev-Key` |
| POST | `/api/reports/{id}/resolve` | `X-Dev-Key` — status: `declined`, `to_be_fixed`, `fixed` + optional message |
| GET | `/api/reports/{id}/resolution` | none — PES3-Disc polls this for user notifications |

## Local API dev

```powershell
cd services/PES3.BugReports.Api
$env:DEV_API_KEY = "dev-local-key"
$env:DATABASE_PATH = "reports.db"
dotnet run
```

See also: shared client library [`PES3-Disc.BugReports`](../src/PES3-Disc.BugReports/).
