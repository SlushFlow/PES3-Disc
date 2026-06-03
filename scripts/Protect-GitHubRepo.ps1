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
    gh auth status 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Not logged in. Run: gh auth login'
    }
}

function Invoke-GhApi {
    param([string]$Method, [string]$Path, [hashtable]$Body = @{})
    $args = @('api', '--method', $Method, $Path)
    foreach ($key in $Body.Keys) {
        $val = $Body[$key]
        if ($val -is [bool]) {
            if ($val) { $args += '-F'; $args += "${key}=true" }
            else { $args += '-F'; $args += "${key}=false" }
        }
        elseif ($null -eq $val) {
            $args += '-f'; $args += "${key}=null"
        }
        else {
            $args += '-f'; $args += "${key}=$val"
        }
    }
    Write-Host "==> $Method $Path" -ForegroundColor Cyan
    & gh @args
    if ($LASTEXITCODE -ne 0) { throw "gh api failed: $Method $Path" }
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
}

# --- Actions: only workflows in this repo; pin action SHAs; read-only default token ---
Invoke-GhApi -Method PUT -Path "repos/$Repo/actions/permissions" -Body @{
    enabled              = $true
    allowed_actions      = 'local_only'
    sha_pinning_required = $true
}

Invoke-GhApi -Method PUT -Path "repos/$Repo/actions/permissions/workflow" -Body @{
    default_workflow_permissions      = 'read'
    can_approve_pull_request_reviews  = $false
}

# Do not run workflows from fork PRs (prevents malicious PR Actions on public repos).
Invoke-GhApi -Method PUT -Path "repos/$Repo/actions/permissions/fork-pr-contributor" -Body @{
    run_workflows_from_fork_pull_requests = $false
}

# --- Branch protection on main ---
# Requires PR + 1 approval + CODEOWNER; no force-push/delete; applies to admins too.
& gh api --method PUT "repos/$Repo/branches/$Branch/protection" `
    -f required_status_checks=null `
    -F enforce_admins=true `
    -F required_pull_request_reviews[required_approving_review_count]=1 `
    -F required_pull_request_reviews[dismiss_stale_reviews]=true `
    -F required_pull_request_reviews[require_code_owner_reviews]=true `
    -F required_pull_request_reviews[require_last_push_approval]=true `
    -f restrictions=null `
    -F allow_force_pushes=false `
    -F allow_deletions=false `
    -F required_conversation_resolution=true
if ($LASTEXITCODE -ne 0) { throw 'Branch protection update failed' }

Write-Host ''
Write-Host 'Done. Summary of protections applied:' -ForegroundColor Green
Write-Host '  - main: PR required, 1 approval, CODEOWNER review, no force-push/delete, admins included'
Write-Host '  - Actions: local workflows only, SHA pinning, read-only GITHUB_TOKEN, no fork PR workflows'
Write-Host '  - Wiki/projects disabled; squash-only merges; delete branch on merge'
Write-Host ''
Write-Host 'Note: Public repos can still be forked and receive PRs/issues from anyone.'
Write-Host 'Only collaborators can push branches; strangers cannot edit main directly.'
Write-Host 'Review Settings > Collaborators and enable "Private vulnerability reporting" manually if desired.'
