# Pull requests from Cloud Agents

## Do not use Cursor "ManagePullRequest" for this repo

That tool opens PRs under the **repository owner's GitHub login** (e.g. `SlushFlow`). With branch protection, the owner **cannot approve their own PR**, so **Merge** stays disabled.

## What to use instead

1. Push to a branch matching `cursor/**` (e.g. `cursor/my-feature-62e5`).
2. GitHub Actions workflow **Open Cursor branch PR** creates a PR as **github-actions[bot]**.
3. The repo owner approves and squash-merges that PR.

## One-time repo setup (owner)

1. **Settings → Actions → General → Workflow permissions**
   - Choose **Read and write permissions**
   - Enable **Allow GitHub Actions to create and approve pull requests**  
     (required; without this, the workflow fails with `GitHub Actions is not permitted to create or approve pull requests`.)

2. Run `scripts/configure-github-branch-access.sh` **or** disable required reviews / allow admin bypass on `main`.

3. **Close PR #9** (opened under `SlushFlow` via Cloud Agent — you cannot approve your own PR).

4. Re-run the failed **Open Cursor branch PR** workflow on branch `cursor/agent-remaster-pr-62e5`, or push an empty commit to that branch. The new PR will be authored by **github-actions[bot]**.

## Direct push to main

After branch protection is relaxed, agents can also `git push origin main` when appropriate.
