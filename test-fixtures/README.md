# PES3-Disc test fixtures

Two temporary disc layouts for local testing (not real games).

| Folder | Simulates | Title ID |
|--------|-----------|----------|
| `diy-demo-disc/` | Decrypted DIY / burned disc | `BLUS99991` |
| `retail-encrypted-disc/` | Official retail (encrypted EBOOT) | `BLUS99992` |

Rebuild:

```powershell
powershell -ExecutionPolicy Bypass -File test-fixtures\Build-TestFixtures.ps1
```

Full integration test (uses these folders as virtual volumes):

```powershell
powershell -ExecutionPolicy Bypass -File Test-PES3-Integration.ps1
```
