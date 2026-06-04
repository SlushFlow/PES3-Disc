# Fix: "GitHub Actions is not permitted to create or approve pull requests"

This error means the repository blocks `GITHUB_TOKEN` from opening PRs. **Only the repo owner can fix it** (Cloud Agents cannot change this setting).

## Steps (about 30 seconds)

1. Open **Repository settings → Actions → General**:  
   https://github.com/SlushFlow/PES3-Disc/settings/actions

2. Scroll to **Workflow permissions**.

3. Select **Read and write permissions** (not "Read repository contents only").

4. Check **Allow GitHub Actions to create and approve pull requests**.

5. Click **Save**.

6. Re-run the failed workflow:  
   https://github.com/SlushFlow/PES3-Disc/actions/workflows/cursor-branch-pull-request.yml  
   → **Run workflow** → branch `cursor/agent-remaster-pr-62e5` → **Run workflow**

The new PR will be authored by **github-actions[bot]** so you can approve and squash-merge it.

## If the checkbox is missing or greyed out

- **Organization-owned repo:** an org owner must allow Actions to create PRs in org settings.
- **Shortcut:** run `scripts/configure-github-branch-access.sh` and push/merge to `main` without a PR.

## Manual fallback (still opens PR as you)

If you cannot enable the setting, use the compare link from the workflow summary — note that GitHub will still list **you** as the PR author if you click "Create pull request".
