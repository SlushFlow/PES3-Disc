#Requires -Version 5.1
<#
.SYNOPSIS
  Hardens SlushFlow/PES3-Disc on GitHub (branch protection, Actions, repo options).

  Run once after: gh auth login
  Requires: repo admin on PES3-Disc

  Example:
    gh auth login
    powershell -ExecutionPolicy Bypass -File scripts/Protect-GitHubRepo.ps1
#>
$ErrorActionPreference = 'Stop'

$Repo = 'SlushFlow/PES3-Disc'
$Branch = 'main'

function Require-Gh {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw 'GitHub CLI (gh) is not installed. Install from https://cli.github.com/'
    }
    $saved = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        gh auth status 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'Not logged in. Run: gh auth login'
        }
    }
    finally {
        $ErrorActionPreference = $saved
    }
}

function Invoke-GhApi {
    param(
        [string]$Method,
        [string]$Path,
        [hashtable]$Body = @{},
        [switch]$Optional
    )

    $ghArgs = @('api', '--method', $Method, $Path)
    foreach ($key in $Body.Keys) {
        $val = $Body[$key]
        if ($val -is [bool]) {
            if ($val) { $ghArgs += '-F'; $ghArgs += "${key}=true" }
            else { $ghArgs += '-F'; $ghArgs += "${key}=false" }
        }
        elseif ($null -eq $val) {
            $ghArgs += '-f'; $ghArgs += "${key}=null"
        }
        else {
            $ghArgs += '-f'; $ghArgs += "${key}=$val"
        }
    }

    Write-Host "==> $Method $Path" -ForegroundColor Cyan

    $saved = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & gh @ghArgs 2>&1
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $saved
    }

    if ($null -ne $output) {
        foreach ($line in @($output)) {
            Write-Host $line
        }
    }

    if ($code -ne 0) {
        if ($Optional) {
            Write-Host "WARN: optional setting not applied ($Path)" -ForegroundColor Yellow
            return $false
        }
        throw "gh api failed: $Method $Path (exit $code)"
    }

    return $true
}

function Invoke-GhApiRaw {
    param([string[]]$GhArgs, [switch]$Optional)

    Write-Host "==> gh $($GhArgs -join ' ')" -ForegroundColor Cyan

    $saved = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & gh @GhArgs 2>&1
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $saved
    }

    if ($null -ne $output) {
        foreach ($line in @($output)) {
            Write-Host $line
        }
    }

    if ($code -ne 0) {
        if ($Optional) {
            Write-Host 'WARN: optional gh command not applied' -ForegroundColor Yellow
            return $false
        }
        throw "gh failed (exit $code)"
    }

    return $true
}

Require-Gh

Write-Host "Hardening $Repo ..." -ForegroundColor Green

# --- Repository options (wiki/projects, merge settings) ---
Invoke-GhApi -Method PATCH -Path "repos/$Repo" -Body @{
    has_wiki                     = $false
    has_projects                 = $false
    delete_branch_on_merge       = $true
    allow_squash_merge           = $true
    allow_merge_commit           = $false
    allow_rebase_merge           = $false
    allow_auto_merge             = $false
    allow_update_branch          = $false
    web_commit_signoff_required  = $false
} | Out-Null

# --- Actions: only workflows in this repo; pin action SHAs; read-only default token ---
Invoke-GhApi -Method PUT -Path "repos/$Repo/actions/permissions" -Body @{
    enabled              = $true
    allowed_actions      = 'local_only'
    sha_pinning_required = $true
} | Out-Null

Invoke-GhApi -Method PUT -Path "repos/$Repo/actions/permissions/workflow" -Body @{
    default_workflow_permissions      = 'read'
    can_approve_pull_request_reviews  = $false
} | Out-Null

# Fork PR approval policy (public repos). Some tokens/plans return 422 — then set in UI.
$forkApprovalOk = Invoke-GhApi -Method PUT -Path "repos/$Repo/actions/permissions/fork-pr-contributor-approval" -Body @{
    approval_policy = 'all_external_contributors'
} -Optional

# --- Branch protection on main ---
Invoke-GhApiRaw -GhArgs @(
    'api', '--method', 'PUT', "repos/$Repo/branches/$Branch/protection",
    '-f', 'required_status_checks=null',
    '-F', 'enforce_admins=true',
    '-F', 'required_pull_request_reviews[required_approving_review_count]=1',
    '-F', 'required_pull_request_reviews[dismiss_stale_reviews]=true',
    '-F', 'required_pull_request_reviews[require_code_owner_reviews]=true',
    '-F', 'required_pull_request_reviews[require_last_push_approval]=true',
    '-f', 'restrictions=null',
    '-F', 'allow_force_pushes=false',
    '-F', 'allow_deletions=false',
    '-F', 'required_conversation_resolution=true'
) | Out-Null

Write-Host ''
Write-Host 'Done. Summary of protections applied:' -ForegroundColor Green
Write-Host '  - main: PR required, 1 approval, CODEOWNER review, no force-push/delete, admins included'
Write-Host '  - Actions: local workflows only, SHA pinning, read-only GITHUB_TOKEN'
if ($forkApprovalOk) {
    Write-Host '  - Fork PR workflows: require approval for all external contributors'
}
else {
    Write-Host '  - Fork PR workflows: set manually (public repo API may reject this):'
    Write-Host '      Settings > Actions > General > Fork pull request workflows'
    Write-Host '      -> Require approval for all external contributors'
}
Write-Host '  - Wiki/projects disabled; squash-only merges; delete branch on merge'
Write-Host ''
Write-Host 'Note: Public repos can still be forked and receive PRs/issues from anyone.'
Write-Host 'Workflows here do not use pull_request triggers from forks; only maintainers push to main.'
Write-Host 'Review Settings > Collaborators and enable "Private vulnerability reporting" manually if desired.'
