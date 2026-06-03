# Privacy — bug reports

PES3-Disc can optionally send **bug reports** to a remote API (default URL is configured in the app; you can change or disable it in Settings).

## Data sent when you submit a report

| Field | Example | Purpose |
|-------|---------|---------|
| Title | Short summary | Triage |
| Body | Description | Triage |
| Platform | `windows` or `linux` | Context |
| App version | e.g. `1.0.0` | Context |
| OS description | e.g. `Microsoft Windows NT 10.0.19045.0` | Context |

No save data, RPCS3 paths, disc contents, or encryption keys are transmitted by this feature.

## After submit

The app stores the report ID locally and polls the API for a **developer response** (declined / to be fixed / fixed + optional message). Only the report ID is required for polling.

## Dev Client

The separate PES3 Dev Client uses a private API key to read all reports; that key must never be embedded in PES3-Disc builds shipped to users.

## Data retention

Retention depends on how the API host is configured (see project maintainer). Self-hosted deployments control their own database.

## Opt out

Leave the bug report API URL empty in Settings, or do not use **Report bug**.

## Local decrypt and cache

Retail decrypt and DIY copy features write game files only to folders on **your computer** (PES3 cache under RPCS3 or a path you choose). PES3-Disc does not upload disc contents, decrypted files, or IRD keys as part of normal play/decrypt. You are responsible for securing and deleting those local files. See [LEGAL.md](LEGAL.md) and [docs/USER-LEGAL-GUIDE.md](docs/USER-LEGAL-GUIDE.md).
