# Bug report API (Render)

PES3-Disc submits bug reports to a small ASP.NET API. The API source and Render deploy config live in **this repo** so GitHub/Render can build them.

| File | Purpose |
|------|---------|
| [`render.yaml`](../render.yaml) | Render Blueprint — web service config |
| [`Dockerfile`](../Dockerfile) | Builds `services/PES3.BugReports.Api` |
| [`services/PES3.BugReports.Api/`](PES3.BugReports.Api/) | REST API (SQLite, clustering, rate limit) |

The **PES3 Dev Client** is a local-only WPF app (workspace `C:\Users\Maksim\PES3-Dev` on the maintainer machine — not in this GitHub repo). It reads summaries, resolves reports, and sets **dev status** for the Windows/Linux apps. Only the API is deployed from **PES3-Disc** on Render.

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
| GET | `/api/dev-status` | none — effective status for PES3-Disc UI (green / yellow / grey dot) |
| PUT | `/api/dev-status` | `X-Dev-Key` — body `{ "mode": "auto" \| "green" \| "yellow" \| "grey" }` |

### Dev status (PES3-Disc Windows & Linux)

The desktop apps show a **Developer** badge with a colored dot:

| Dot | Meaning |
|-----|---------|
| Green | At home and working |
| Yellow | Break or vacation (manual only) |
| Grey | Nighttime / day off |

**Automatic schedule (Eastern Time)** when mode is `auto`:

- **Green** from 8:00 AM through 9:59 PM ET  
- **Grey** from 10:00 PM through 7:59 AM ET  

Use the **PES3 Dev Client** at `C:\Users\Maksim\PES3-Dev` → run `Build-DevClient.ps1` or `dotnet run` in `PES3.DevClient` → set **Auto**, **Green**, **Yellow**, or **Grey**. Manual modes override the schedule until you choose **Auto** again.

## Local API dev

```powershell
cd services/PES3.BugReports.Api
$env:DEV_API_KEY = "dev-local-key"
$env:DATABASE_PATH = "reports.db"
dotnet run
```

See also: shared client library [`PES3-Disc.BugReports`](../src/PES3-Disc.BugReports/).
