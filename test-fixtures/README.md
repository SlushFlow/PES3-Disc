# PES3-Disc test fixtures

Two mock PS3 disc layouts for automated testing (not real games).

| Folder | Simulates | Title ID | EBOOT |
|--------|-----------|----------|-------|
| `diy-demo-disc/` | DIY / burned disc (decrypted files) | `BLUS99991` | ELF (playable) |
| `retail-encrypted-disc/` | Official retail disc | `BLUS99992` | SCE (encrypted) |

## Rebuild fixtures

**Windows (PowerShell):**

```powershell
powershell -ExecutionPolicy Bypass -File test-fixtures\Build-TestFixtures.ps1
```

**Any OS (via dotnet tests — also runs in CI):**

```bash
dotnet test tests/PES3-Disc.Core.Tests/PES3-Disc.Core.Tests.csproj --filter Committed_diy
```

The C# fixture builder in `tests/PES3-Disc.Core.Tests/Fixtures/Ps3DiscFixtureBuilder.cs` writes both layouts cross-platform.

## Run all tests

**Windows:**

```powershell
./scripts/ci-test-windows.ps1
```

**Linux:**

```bash
chmod +x scripts/ci-test-linux.sh
./scripts/ci-test-linux.sh
```

**Quick integration (PowerShell, ~1 min with `-Quick`):**

```powershell
./Test-PES3-Integration.ps1 -Quick
```

## CI

GitHub Actions workflow `.github/workflows/ci.yml` runs the full suite on **windows-latest** and **ubuntu-latest** on every push/PR to `main`.

Covers: disc detection (DIY + retail), cache, bug-report API, dev status, legal terms, PowerShell integration (Windows), and Linux CLI `scan --test-volume`.
