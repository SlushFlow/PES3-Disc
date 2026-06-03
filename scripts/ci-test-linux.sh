#!/usr/bin/env bash
# Cross-platform CI test runner for Linux (fixtures, dotnet test, CLI scan).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "==> Build test fixtures (dotnet)"
dotnet --list-sdks
dotnet build src/PES3-Disc.Core/PES3-Disc.Core.csproj -c Release -v q -p:LangVersion=12
dotnet build tests/PES3-Disc.Core.Tests/PES3-Disc.Core.Tests.csproj -c Release -v q -p:LangVersion=12

echo "==> Run dotnet tests"
dotnet test tests/PES3-Disc.Core.Tests/PES3-Disc.Core.Tests.csproj -c Release --no-build -v n --logger "console;verbosity=normal"
dotnet build tests/PES3.BugReports.Api.Tests/PES3.BugReports.Api.Tests.csproj -c Release -v q -p:LangVersion=12
dotnet test tests/PES3.BugReports.Api.Tests/PES3.BugReports.Api.Tests.csproj -c Release --no-build -v n --logger "console;verbosity=normal"

echo "==> Build Linux CLI"
dotnet build src/PES3-Disc.Cli/PES3-Disc.Cli.csproj -c Release -v q

CLI_DLL="$ROOT/src/PES3-Disc.Cli/bin/Release/net8.0/pes3-disc-cli.dll"
DIY="$ROOT/test-fixtures/diy-demo-disc"
RETAIL="$ROOT/test-fixtures/retail-encrypted-disc"

if [[ ! -f "$DIY/PS3_GAME/USRDIR/EBOOT.BIN" ]]; then
  echo "FAIL: DIY fixture missing after tests" >&2
  exit 1
fi

echo "==> CLI scan --test-volume (DIY + retail fixtures)"
OUT="$(dotnet exec "$CLI_DLL" scan --test-volume "$DIY" --test-volume "$RETAIL")"
echo "$OUT"

echo "$OUT" | grep -qi "PES3 DIY Test Disc" || { echo "FAIL: DIY not in CLI scan output" >&2; exit 1; }
echo "$OUT" | grep -qi "EncryptedRetail\|Encrypted retail\|decryption" || { echo "FAIL: retail not in CLI scan output" >&2; exit 1; }

echo "==> CLI help smoke"
dotnet exec "$CLI_DLL" help | grep -qi "pes3-disc"

echo "PASS: Linux CI tests completed"
