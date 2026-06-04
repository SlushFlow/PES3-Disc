#!/usr/bin/env bash
# Relax main branch protection so repo owners and CI/agents can push and merge without PR approval.
# Must run locally as a user with admin on the repo (e.g. SlushFlow):
#   gh auth login
#   ./scripts/configure-github-branch-access.sh
set -euo pipefail

OWNER="${GITHUB_REPO_OWNER:-SlushFlow}"
REPO="${GITHUB_REPO_NAME:-PES3-Disc}"
BRANCH="${GITHUB_BRANCH:-main}"

echo "==> GitHub identity (must be repo admin: $OWNER)"
if ! gh api user --jq '.login' 2>/dev/null; then
  echo "ERROR: gh is not logged in with a user token. Run: gh auth login" >&2
  exit 1
fi

LOGIN="$(gh api user --jq '.login')"
ADMIN="$(gh api "repos/$OWNER/$REPO" --jq '.permissions.admin' 2>/dev/null || echo false)"
if [[ "$ADMIN" != "true" ]]; then
  echo "ERROR: $LOGIN does not have admin on $OWNER/$REPO. Log in as the owner and retry." >&2
  exit 1
fi

echo "==> Configuring $OWNER/$REPO branch: $BRANCH"

# 1) Repository rulesets (GitHub Rules) — disable or soften rules on main
RULESETS="$(gh api "repos/$OWNER/$REPO/rulesets" --jq '.[].id' 2>/dev/null || true)"
if [[ -n "$RULESETS" ]]; then
  while read -r id; do
    [[ -z "$id" ]] && continue
    echo "    Updating ruleset $id (disable required PR / reviews if present)"
    gh api "repos/$OWNER/$REPO/rulesets/$id" --jq '.name' 2>/dev/null || true
    # Set enforcement to disabled for solo-dev agent workflow (owner can re-enable later)
    gh api -X PUT "repos/$OWNER/$REPO/rulesets/$id" \
      -f enforcement=disabled 2>/dev/null \
      && echo "    Disabled ruleset $id" \
      || echo "    Could not auto-update ruleset $id — edit manually under Settings → Rules"
  done <<< "$RULESETS"
fi

# 2) Classic branch protection — remove so push/merge to main does not require approval
if gh api "repos/$OWNER/$REPO/branches/$BRANCH/protection" &>/dev/null; then
  echo "==> Removing classic branch protection on $BRANCH"
  gh api -X DELETE "repos/$OWNER/$REPO/branches/$BRANCH/protection"
  echo "    Deleted branch protection on $BRANCH"
else
  echo "==> No classic branch protection on $BRANCH (or already removed)"
fi

# 3) Optional: light protection without PR reviews (comment out block 2 and use this instead)
# gh api -X PUT "repos/$OWNER/$REPO/branches/$BRANCH/protection" --input - <<'JSON'
# {
#   "required_status_checks": null,
#   "enforce_admins": false,
#   "required_pull_request_reviews": null,
#   "restrictions": null,
#   "required_linear_history": false,
#   "allow_force_pushes": false,
#   "allow_deletions": false
# }
# JSON

echo ""
echo "Done. You should now be able to:"
echo "  - Push directly to $BRANCH"
echo "  - Squash-merge PR #9 without an approval"
echo ""
echo "Cursor Cloud Agent: ensure the Cursor GitHub App has Contents + Pull requests"
echo "  (read/write) on this repo under GitHub → Settings → Integrations."
