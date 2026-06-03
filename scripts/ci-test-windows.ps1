#Requires -Version 5.1
# Windows CI test runner: fixtures, dotnet test, PowerShell integration, CLI scan.
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host '==> Build projects (skip DumpCli — needs .NET 10 + ps3-disc-dumper)' -ForegroundColor Cyan
$projects = @(
    'src/PES3-Disc.Core/PES3-Disc.Core.csproj',
    'src/PES3-Disc.BugReports/PES3-Disc.BugReports.csproj',
    'src/PES3-Disc.ViewModels/PES3-Disc.ViewModels.csproj',
    'src/PES3-Disc.App/PES3-Disc.App.csproj',
    'src/PES3-Disc.Cli/PES3-Disc.Cli.csproj',
    'tests/PES3-Disc.Core.Tests/PES3-Disc.Core.Tests.csproj',
    'tests/PES3.BugReports.Api.Tests/PES3.BugReports.Api.Tests.csproj'
)
foreach ($proj in $projects) {
    dotnet build $proj -c Release -v q
}

Write-Host '==> Build test fixtures' -ForegroundColor Cyan
& (Join-Path $Root 'test-fixtures\Build-TestFixtures.ps1')

Write-Host '==> dotnet test' -ForegroundColor Cyan
dotnet test tests/PES3-Disc.Core.Tests/PES3-Disc.Core.Tests.csproj -c Release -v n
dotnet test tests/PES3.BugReports.Api.Tests/PES3.BugReports.Api.Tests.csproj -c Release -v n

Write-Host '==> Layout detection (PowerShell)' -ForegroundColor Cyan
& (Join-Path $Root 'Test-Ps3DiscDetection.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Test-Ps3DiscDetection.ps1 failed' }

Write-Host '==> Integration test (Quick)' -ForegroundColor Cyan
& (Join-Path $Root 'Test-PES3-Integration.ps1') -Quick
if ($LASTEXITCODE -ne 0) { throw 'Test-PES3-Integration.ps1 failed' }

Write-Host '==> CLI scan --test-volume' -ForegroundColor Cyan
$cli = Join-Path $Root 'src\PES3-Disc.Cli\bin\Release\net8.0\pes3-disc-cli.dll'
$diy = Join-Path $Root 'test-fixtures\diy-demo-disc'
$retail = Join-Path $Root 'test-fixtures\retail-encrypted-disc'
$out = dotnet exec $cli scan --test-volume $diy --test-volume $retail 2>&1 | Out-String
Write-Host $out
if ($out -notmatch 'PES3 DIY Test Disc') { throw 'CLI did not detect DIY fixture' }
if ($out -notmatch 'EncryptedRetail|Encrypted retail|decryption') { throw 'CLI did not detect retail fixture' }

Write-Host 'PASS: Windows CI tests completed' -ForegroundColor Green
