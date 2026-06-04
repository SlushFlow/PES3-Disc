# Pull requests from Cloud Agents

## Do not use Cursor "ManagePullRequest" for this repo

That tool opens PRs under the **repository owner's GitHub login** (e.g. `SlushFlow`). With branch protection, the owner **cannot approve their own PR**, so **Merge** stays disabled.

## What to use instead

1. Push to a branch matching `cursor/**` (e.g. `cursor/my-feature-62e5`).
2. GitHub Actions workflow **Open Cursor branch PR** creates a PR as **github-actions[bot]**.
3. The repo owner approves and squash-merges that PR.

## One-time repo setup (owner)

- Run `scripts/configure-github-branch-access.sh` **or** disable required reviews / allow admin bypass on `main`.
- Close any duplicate PR that was opened under your personal account for the same branch.

## Direct push to main

After branch protection is relaxed, agents can also `git push origin main` when appropriate.
