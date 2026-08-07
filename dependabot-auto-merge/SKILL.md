---
name: dependabot-auto-merge
description: |
  Configure Dependabot auto-merge for a GitHub repository. Sets up automatic merging of Dependabot pull requests for patch and minor version updates, with optional CI check enforcement. Use this skill whenever the user wants to automate Dependabot PR merging, set up Dependabot workflows, configure branch protection for Dependabot, or enable auto-merge for dependency updates.
compatibility: Requires gh CLI and git repository access
---

# Dependabot Auto-Merge Configuration

This skill automates the setup of Dependabot auto-merge for a GitHub repository. It configures:

1. **Auto-merge enablement** - Allows PRs to be automatically merged when all checks pass
2. **Squash merge** - Enables squash merge strategy for cleaner commit history
3. **Branch ruleset** - Creates a ruleset for the default branch with:
   - Deletion protection (prevents accidental branch deletion)
   - Non-fast-forward protection (prevents force pushes)
   - Optional CI status checks (if specified)
4. **Workflow files** - Generates Dependabot configuration and auto-merge workflow

## What You Need

- A GitHub repository with Dependabot enabled
- `gh` CLI installed and authenticated
- Write access to the repository

## How to Use

Provide the repository name in `owner/repo` format. Optionally specify CI check names that must pass before auto-merge (e.g., `build`, `test`).

### Example

```
Configure Dependabot auto-merge for tsubakimoto/turbo-train with CI checks: build, test
```

If CI checks are not specified, the ruleset will only enforce deletion and force-push protection.

## Output

The skill generates:

1. **Repository configuration** - Auto-merge and squash merge enabled
2. **Branch ruleset** - Protection rules for the default branch
3. **Workflow file**:
   - `.github/workflows/dependabot-auto-merge.yml` - Workflow for auto-merging patch/minor updates

## Next Steps

After running this skill:

1. The auto-merge workflow will be automatically committed and pushed
2. Configure your own `.github/dependabot.yml` if needed (to avoid conflicts)
3. Dependabot will begin creating PRs according to your schedule
4. Patch and minor updates will automatically merge once CI checks pass
5. Major updates will require manual review
