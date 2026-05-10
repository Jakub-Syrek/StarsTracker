# Branch Protection Setup

This document captures the GitHub branch-protection settings the repository
expects. The rules cannot be committed to source — they live on the
`master` branch on GitHub itself — so this file is the source of truth and
has to be applied manually (or via the `gh` CLI / Terraform when we add
infrastructure-as-code).

## Required for `master`

Navigate to **Settings → Branches → Branch protection rules → Add rule**
and configure:

- **Branch name pattern**: `master`
- ☑️ Require a pull request before merging
  - ☑️ Require approvals: **0** (solo project, but reviews encouraged)
  - ☑️ Dismiss stale pull-request approvals when new commits are pushed
- ☑️ Require status checks to pass before merging
  - ☑️ Require branches to be up to date before merging
  - Required checks:
    - `Tests / test`
    - `Tests / code-quality`
- ☑️ Require linear history (we squash-merge feature branches)
- ☑️ Do not allow bypassing the above settings (applies to admins too)
- ☐ Allow force pushes — **disabled**
- ☐ Allow deletions — **disabled**

## Recommended optional settings

- ☑️ Require signed commits (when GPG signing is set up).
- ☑️ Require deployments to succeed before merging — once we have a
  release-build workflow that produces signed APKs.

## Quick CLI setup

Once `gh` is authenticated against the repository:

```powershell
gh api -X PUT repos/Jakub-Syrek/StarsTracker/branches/master/protection `
  -f required_status_checks.strict=true `
  -f 'required_status_checks.contexts[]=Tests / test' `
  -f 'required_status_checks.contexts[]=Tests / code-quality' `
  -F enforce_admins=true `
  -F required_pull_request_reviews.required_approving_review_count=0 `
  -F required_pull_request_reviews.dismiss_stale_reviews=true `
  -F restrictions=
```

## Why these rules

- **Conventional commits** drive the auto-versioning workflow — landing a
  commit that doesn't match feat/fix/docs/etc. would skip the release.
  Branch protection forces every change through CI which validates the
  Polish-string and authorship rules.
- **Signed/verified commits** prevent impersonation; only `Jakub Syrek
  <jakubvonsyrek@gmail.com>` is permitted by the `code-quality` check.
- **No force-push, no deletions** — release tags rely on a stable history.
