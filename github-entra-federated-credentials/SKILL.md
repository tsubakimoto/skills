---
name: github-entra-federated-credentials
description: >
  Sets up Entra ID federated credentials so GitHub Actions can authenticate to Azure
  using OIDC (workload identity federation) — no client secrets required.
  USE THIS SKILL whenever the user wants to: connect GitHub Actions to Azure, configure
  Azure login from GitHub Actions, set up OIDC or workload identity federation between
  GitHub and Azure, avoid storing Azure credentials as GitHub secrets, configure
  Entra ID / Azure AD app registration for a GitHub repository, use azure/login@v2 in
  a workflow, or get a "AADSTS700213: No matching federated identity record" error.
  Invoke this skill even if the user just says "set up Azure access from GitHub",
  "let GitHub Actions deploy to Azure", or "configure my Actions to use Azure".
---

## What this skill does

Automates the complete Entra ID federated credentials setup for a GitHub repository:

1. Creates an Entra app registration and service principal
2. Fetches the GitHub owner ID and repository ID (needed for the correct OIDC subject)
3. Creates a federated credential with the right subject format
4. Creates the GitHub Environment if it doesn't exist
5. Registers `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` as secrets

## Prerequisites

Before running, verify:
- `az` CLI installed and logged in (`az login`)
- `gh` CLI installed and logged in (`gh auth login`)
- User has permission to create app registrations in the Entra tenant

## Inputs to collect

Ask the user for any missing values before running the script:

| Argument | Description | Example |
|---|---|---|
| `--app-name` | Display name for the Entra app | `my-github-actions-app` |
| `--repo` | GitHub repo in `owner/repo` format | `myorg/myapp` |
| `--environment` | GitHub Environment name | `Production` |
| `--resource-group` | Resource group for Contributor role assignment (optional) | `rg-myapp` |

## Running the setup script

Collect all required inputs from the user first, then run the script for their environment.

**Windows (PowerShell):**

```powershell
pwsh "<skill_dir>/scripts/setup.ps1" `
  -AppName "<app name>" `
  -Repo "<owner/repo>" `
  -Environment "<env name>" `
  [-ResourceGroup "<rg name>"]
```

**macOS / Linux (Bash):**

```bash
bash "<skill_dir>/scripts/setup.sh" \
  --app-name "<app name>" \
  --repo "<owner/repo>" \
  --environment "<env name>" \
  [--resource-group "<rg name>"]
```

`<skill_dir>` is the directory containing this SKILL.md file.

## OIDC subject format — why IDs matter

GitHub's OIDC tokens use a subject in this format:
```
repo:{owner}@{owner_id}/{repo}@{repo_id}:environment:{env}
```

**This is not the same as** `repo:{owner}/{repo}:environment:{env}`. The numeric owner and
repository IDs must be included. The script fetches them automatically via `gh api`.
Registering the wrong subject causes `AADSTS700213` errors at runtime.

## After setup

The workflow template printed by the script is ready to use. The three secrets
(`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`) are scoped to the
specified GitHub Environment, so the workflow must declare `environment: <env name>`.

## Troubleshooting

| Error | Likely cause | Fix |
|---|---|---|
| `AADSTS700213` | Subject mismatch in federated credential | Re-run setup; check owner/repo IDs |
| `gh secret set` fails | Environment doesn't exist | Script creates it via `gh api PUT` first |
| `az ad app federated-credential create` fails | Inline JSON quoting issue | Script uses a temp file — should not occur |
| Role assignment fails | Insufficient permissions | Assign manually or use a privileged account |
