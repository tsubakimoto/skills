#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Configure Entra ID federated credentials to allow GitHub Actions to access Azure.

.DESCRIPTION
    Performs the following steps:
      1. Create an Entra app registration
      2. Create a service principal
      3. Configure federated credentials
      4. Assign the Contributor role to a resource group (optional)
      5. Register secrets in the GitHub repository environment

.REQUIREMENTS
    - Azure CLI (az) installed and logged in
    - GitHub CLI (gh) installed and logged in
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─────────────────────────────────────────
# Input
# ─────────────────────────────────────────
Write-Host ""
Write-Host "=== Entra Federated Credentials Setup ===" -ForegroundColor Cyan
Write-Host ""

$appName = Read-Host "Entra app name"
$repo    = Read-Host "Repository (e.g. org/repo)"
$env     = Read-Host "GitHub Environment name"
$rgName  = Read-Host "Resource group name for role assignment (press Enter to skip)"

# ─────────────────────────────────────────
# Prerequisites
# ─────────────────────────────────────────
Write-Host ""
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI (az) not found. Install it: https://aka.ms/installazurecliwindows"
}
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error "GitHub CLI (gh) not found. Install it: https://cli.github.com/"
}

$accountJson = az account show 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "Not logged in to Azure. Run 'az login'."
}

$ghStatus = gh auth status 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "Not logged in to GitHub. Run 'gh auth login'."
}

$subscriptionId = (az account show --query id -o tsv)
$tenantId       = (az account show --query tenantId -o tsv)
Write-Host "Subscription ID : $subscriptionId" -ForegroundColor Gray
Write-Host "Tenant ID       : $tenantId" -ForegroundColor Gray

# ─────────────────────────────────────────
# Create Entra app
# ─────────────────────────────────────────
Write-Host ""
Write-Host "Creating Entra app: $appName ..." -ForegroundColor Yellow

$appJson  = az ad app create --display-name $appName | ConvertFrom-Json
$appId    = $appJson.appId
$appObjId = $appJson.id
Write-Host "App ID (Client ID) : $appId" -ForegroundColor Green

# ─────────────────────────────────────────
# Create service principal
# ─────────────────────────────────────────
Write-Host "Creating service principal..." -ForegroundColor Yellow

$spJson  = az ad sp create --id $appId | ConvertFrom-Json
$spObjId = $spJson.id
Write-Host "SP Object ID : $spObjId" -ForegroundColor Green

# ─────────────────────────────────────────
# Fetch GitHub owner ID and repository ID
# ─────────────────────────────────────────
Write-Host "Fetching GitHub repository info..." -ForegroundColor Yellow

$repoInfo   = gh api "repos/$repo" | ConvertFrom-Json
$ownerId    = $repoInfo.owner.id
$repoId     = $repoInfo.id
$ownerLogin = $repoInfo.owner.login
$repoName   = $repoInfo.name
Write-Host "Owner      : $ownerLogin (ID: $ownerId)" -ForegroundColor Gray
Write-Host "Repository : $repoName (ID: $repoId)" -ForegroundColor Gray

# ─────────────────────────────────────────
# Create federated credential
# ─────────────────────────────────────────
Write-Host "Configuring federated credential..." -ForegroundColor Yellow

# GitHub OIDC token subject format: repo:{owner}@{owner_id}/{repo}@{repo_id}:environment:{env}
$subject  = "repo:${ownerLogin}@${ownerId}/${repoName}@${repoId}:environment:${env}"
$credName = "github-actions-$($env.ToLower() -replace '[^a-z0-9-]', '-')"

# Write to a temp file because az CLI mangles inline JSON quotes on Windows
$tmpCredFile = [System.IO.Path]::GetTempFileName()
@{
    name        = $credName
    issuer      = "https://token.actions.githubusercontent.com"
    subject     = $subject
    description = "Federated credential for GitHub Actions (repo: $repo, env: $env)"
    audiences   = @("api://AzureADTokenExchange")
} | ConvertTo-Json | Set-Content -Path $tmpCredFile -Encoding UTF8

try {
    az ad app federated-credential create --id $appObjId --parameters $tmpCredFile | Out-Null
    Write-Host "Federated credential created (subject: $subject)" -ForegroundColor Green
} finally {
    Remove-Item $tmpCredFile -Force -ErrorAction SilentlyContinue
}

# ─────────────────────────────────────────
# Role assignment (optional)
# ─────────────────────────────────────────
if ($rgName -ne "") {
    Write-Host "Assigning Contributor role (resource group: $rgName) ..." -ForegroundColor Yellow

    $scope = "/subscriptions/$subscriptionId/resourceGroups/$rgName"
    az role assignment create `
        --role contributor `
        --subscription $subscriptionId `
        --assignee-object-id $spObjId `
        --assignee-principal-type ServicePrincipal `
        --scope $scope | Out-Null

    Write-Host "Role assignment complete (scope: $scope)" -ForegroundColor Green
} else {
    Write-Host "Role assignment skipped. Configure manually if needed." -ForegroundColor Gray
}

# ─────────────────────────────────────────
# Create GitHub Environment
# ─────────────────────────────────────────
Write-Host "Creating GitHub Environment: $env ..." -ForegroundColor Yellow

# PUT is idempotent — safe to call even if the environment already exists
gh api "repos/$repo/environments/$env" --method PUT | Out-Null
Write-Host "GitHub Environment created: $env" -ForegroundColor Green

# ─────────────────────────────────────────
# Register GitHub secrets
# ─────────────────────────────────────────
Write-Host "Registering GitHub secrets (repo: $repo, env: $env) ..." -ForegroundColor Yellow

gh secret set AZURE_CLIENT_ID       --body $appId          --repo $repo --env $env
gh secret set AZURE_TENANT_ID       --body $tenantId       --repo $repo --env $env
gh secret set AZURE_SUBSCRIPTION_ID --body $subscriptionId --repo $repo --env $env

Write-Host "GitHub secrets registered:" -ForegroundColor Green
Write-Host "  AZURE_CLIENT_ID       = $appId" -ForegroundColor Gray
Write-Host "  AZURE_TENANT_ID       = $tenantId" -ForegroundColor Gray
Write-Host "  AZURE_SUBSCRIPTION_ID = $subscriptionId" -ForegroundColor Gray

# ─────────────────────────────────────────
# Summary
# ─────────────────────────────────────────
Write-Host ""
Write-Host "=== Setup Complete ===" -ForegroundColor Cyan
Write-Host "Use the following configuration in your GitHub Actions workflow:" -ForegroundColor White
Write-Host ""
Write-Host @"
name: Connect to Azure

on:
  workflow_dispatch:

permissions:
  id-token: write
  contents: read

jobs:
  deploy:
    runs-on: ubuntu-slim
    environment: $env
    steps:
      - uses: azure/login@v2
        with:
          client-id: `${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: `${{ secrets.AZURE_TENANT_ID }}
          subscription-id: `${{ secrets.AZURE_SUBSCRIPTION_ID }}
"@ -ForegroundColor DarkCyan
Write-Host ""
