#!/usr/bin/env bash
# =============================================================================
# Entra ID Federated Credentials Setup Script
#
# Steps:
#   1. Create an Entra app registration
#   2. Create a service principal
#   3. Configure federated credentials
#   4. Assign the Contributor role to a resource group (optional)
#   5. Register secrets in the GitHub repository environment
#
# Requirements:
#   - Azure CLI (az) installed and logged in
#   - GitHub CLI (gh) installed and logged in
# =============================================================================

set -euo pipefail

# ─────────────────────────────────────────
# Input
# ─────────────────────────────────────────
echo ""
echo "=== Entra Federated Credentials Setup ==="
echo ""

read -rp "Entra app name: "                                    APP_NAME
read -rp "Repository (e.g. org/repo): "                        REPO
read -rp "GitHub Environment name: "                           ENV_NAME
read -rp "Resource group name for role assignment (Enter to skip): " RG_NAME

# ─────────────────────────────────────────
# Prerequisites
# ─────────────────────────────────────────
echo ""
echo "Checking prerequisites..."

if ! command -v az &>/dev/null; then
    echo "ERROR: Azure CLI (az) not found. Install it: https://aka.ms/installazurecli" >&2
    exit 1
fi
if ! command -v gh &>/dev/null; then
    echo "ERROR: GitHub CLI (gh) not found. Install it: https://cli.github.com/" >&2
    exit 1
fi

if ! az account show &>/dev/null; then
    echo "ERROR: Not logged in to Azure. Run 'az login'." >&2
    exit 1
fi

if ! gh auth status &>/dev/null; then
    echo "ERROR: Not logged in to GitHub. Run 'gh auth login'." >&2
    exit 1
fi

SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
echo "Subscription ID : ${SUBSCRIPTION_ID}"
echo "Tenant ID       : ${TENANT_ID}"

# ─────────────────────────────────────────
# Create Entra app
# ─────────────────────────────────────────
echo ""
echo "Creating Entra app: ${APP_NAME} ..."

APP_JSON=$(az ad app create --display-name "${APP_NAME}")
APP_ID=$(echo "${APP_JSON}"     | python3 -c "import sys,json; print(json.load(sys.stdin)['appId'])")
APP_OBJ_ID=$(echo "${APP_JSON}" | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
echo "App ID (Client ID) : ${APP_ID}"

# ─────────────────────────────────────────
# Create service principal
# ─────────────────────────────────────────
echo "Creating service principal..."

SP_JSON=$(az ad sp create --id "${APP_ID}")
SP_OBJ_ID=$(echo "${SP_JSON}" | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
echo "SP Object ID : ${SP_OBJ_ID}"

# ─────────────────────────────────────────
# Fetch GitHub owner ID and repository ID
# ─────────────────────────────────────────
echo "Fetching GitHub repository info..."

REPO_JSON=$(gh api "repos/${REPO}")
OWNER_LOGIN=$(echo "${REPO_JSON}" | python3 -c "import sys,json; print(json.load(sys.stdin)['owner']['login'])")
OWNER_ID=$(echo "${REPO_JSON}"    | python3 -c "import sys,json; print(json.load(sys.stdin)['owner']['id'])")
REPO_NAME=$(echo "${REPO_JSON}"   | python3 -c "import sys,json; print(json.load(sys.stdin)['name'])")
REPO_ID=$(echo "${REPO_JSON}"     | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
echo "Owner      : ${OWNER_LOGIN} (ID: ${OWNER_ID})"
echo "Repository : ${REPO_NAME} (ID: ${REPO_ID})"

# ─────────────────────────────────────────
# Create federated credential
# ─────────────────────────────────────────
echo "Configuring federated credential..."

# GitHub OIDC token subject format: repo:{owner}@{owner_id}/{repo}@{repo_id}:environment:{env}
SUBJECT="repo:${OWNER_LOGIN}@${OWNER_ID}/${REPO_NAME}@${REPO_ID}:environment:${ENV_NAME}"
CRED_NAME="github-actions-$(echo "${ENV_NAME}" | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9-]/-/g')"

# Write to a temp file because az CLI mangles inline JSON quotes
TMP_CRED_FILE=$(mktemp)
cat > "${TMP_CRED_FILE}" <<JSON
{
  "name": "${CRED_NAME}",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "${SUBJECT}",
  "description": "Federated credential for GitHub Actions (repo: ${REPO}, env: ${ENV_NAME})",
  "audiences": ["api://AzureADTokenExchange"]
}
JSON

az ad app federated-credential create --id "${APP_OBJ_ID}" --parameters "${TMP_CRED_FILE}" >/dev/null
rm -f "${TMP_CRED_FILE}"
echo "Federated credential created (subject: ${SUBJECT})"

# ─────────────────────────────────────────
# Role assignment (optional)
# ─────────────────────────────────────────
if [[ -n "${RG_NAME}" ]]; then
    echo "Assigning Contributor role (resource group: ${RG_NAME}) ..."

    SCOPE="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RG_NAME}"
    az role assignment create \
        --role contributor \
        --subscription "${SUBSCRIPTION_ID}" \
        --assignee-object-id "${SP_OBJ_ID}" \
        --assignee-principal-type ServicePrincipal \
        --scope "${SCOPE}" >/dev/null

    echo "Role assignment complete (scope: ${SCOPE})"
else
    echo "Role assignment skipped. Configure manually if needed."
fi

# ─────────────────────────────────────────
# Create GitHub Environment
# ─────────────────────────────────────────
echo "Creating GitHub Environment: ${ENV_NAME} ..."

# PUT is idempotent — safe to call even if the environment already exists
gh api "repos/${REPO}/environments/${ENV_NAME}" --method PUT >/dev/null
echo "GitHub Environment created: ${ENV_NAME}"

# ─────────────────────────────────────────
# Register GitHub secrets
# ─────────────────────────────────────────
echo "Registering GitHub secrets (repo: ${REPO}, env: ${ENV_NAME}) ..."

gh secret set AZURE_CLIENT_ID       --body "${APP_ID}"          --repo "${REPO}" --env "${ENV_NAME}"
gh secret set AZURE_TENANT_ID       --body "${TENANT_ID}"       --repo "${REPO}" --env "${ENV_NAME}"
gh secret set AZURE_SUBSCRIPTION_ID --body "${SUBSCRIPTION_ID}" --repo "${REPO}" --env "${ENV_NAME}"

echo "GitHub secrets registered:"
echo "  AZURE_CLIENT_ID       = ${APP_ID}"
echo "  AZURE_TENANT_ID       = ${TENANT_ID}"
echo "  AZURE_SUBSCRIPTION_ID = ${SUBSCRIPTION_ID}"

# ─────────────────────────────────────────
# Summary
# ─────────────────────────────────────────
echo ""
echo "=== Setup Complete ==="
echo "Use the following configuration in your GitHub Actions workflow:"
echo ""
cat <<EOF
name: Connect to Azure

on:
  workflow_dispatch:

permissions:
  id-token: write
  contents: read

jobs:
  deploy:
    runs-on: ubuntu-slim
    environment: ${ENV_NAME}
    steps:
      - uses: azure/login@v2
        with:
          client-id: \${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: \${{ secrets.AZURE_TENANT_ID }}
          subscription-id: \${{ secrets.AZURE_SUBSCRIPTION_ID }}
EOF
echo ""
