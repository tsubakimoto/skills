#!/bin/bash

# Dependabot auto-merge setup script
# Usage: ./setup.sh owner/repo [ci-check-1] [ci-check-2] ...

set -e

# Check if gh CLI is installed
if ! command -v gh &> /dev/null; then
  echo "❌ Error: GitHub CLI (gh) is not installed."
  echo ""
  echo "Please install it first:"
  echo "  macOS:  brew install gh"
  echo "  Linux:  https://github.com/cli/cli/blob/trunk/docs/install_linux.md"
  echo "  Windows: choco install gh"
  echo ""
  echo "After installation, authenticate with: gh auth login"
  exit 1
fi

# Check if git is installed
if ! command -v git &> /dev/null; then
  echo "❌ Error: Git is not installed."
  echo ""
  echo "Please install it first:"
  echo "  macOS:  brew install git"
  echo "  Linux:  sudo apt-get install git"
  echo "  Windows: https://git-scm.com/download/win"
  exit 1
fi

REPO="${1:?Repository required (e.g., owner/repo)}"
shift || true
CI_CHECKS=("$@")

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSETS_DIR="$(dirname "$SCRIPT_DIR")/assets"

echo "Setting up Dependabot auto-merge for: $REPO"

# Enable auto-merge
echo "Enabling auto-merge..."
gh repo edit "$REPO" --enable-auto-merge

# Enable squash merge
echo "Enabling squash merge..."
gh repo edit "$REPO" --enable-squash-merge

# Set default branch to main
echo "Setting default branch to main..."
gh repo edit "$REPO" --default-branch main

# Create ruleset
echo "Creating branch ruleset..."

# Build the rules array
RULES='[
  {"type": "deletion"},
  {"type": "non_fast_forward"}
]'

# Add CI checks if provided
if [ ${#CI_CHECKS[@]} -gt 0 ]; then
  STATUS_CHECKS="["
  for i in "${!CI_CHECKS[@]}"; do
    if [ $i -gt 0 ]; then
      STATUS_CHECKS+=","
    fi
    STATUS_CHECKS+="{\"context\": \"${CI_CHECKS[$i]}\", \"integration_id\": null}"
  done
  STATUS_CHECKS+="]"
  
  RULES=$(cat <<EOF
[
  {"type": "deletion"},
  {"type": "non_fast_forward"},
  {
    "type": "required_status_checks",
    "parameters": {
      "required_status_checks": $STATUS_CHECKS,
      "strict_required_status_checks_policy": true
    }
  }
]
EOF
)
fi

# Create the complete ruleset JSON
RULESET=$(cat <<EOF
{
  "name": "Main branch protection",
  "target": "branch",
  "enforcement": "active",
  "conditions": {
    "ref_name": {
      "include": ["~DEFAULT_BRANCH"],
      "exclude": []
    }
  },
  "rules": $RULES
}
EOF
)

# Send to GitHub API
echo "$RULESET" | gh api repos/"$REPO"/rulesets -X POST --input -

echo "✓ Repository configuration completed!"

# Clone repository and add configuration files
echo ""
echo "Cloning repository and adding configuration files..."

TEMP_DIR=$(mktemp -d)
trap "rm -rf $TEMP_DIR" EXIT

cd "$TEMP_DIR"
gh repo clone "$REPO" .

# Create .github directories if they don't exist
mkdir -p .github/workflows

# Copy configuration files
cp "$ASSETS_DIR/dependabot-auto-merge.yml" .github/workflows/dependabot-auto-merge.yml

# Commit and push
git config user.name "github-actions[bot]"
git config user.email "github-actions[bot]@users.noreply.github.com"

git add .github/workflows/dependabot-auto-merge.yml
git commit -m "chore: add Dependabot auto-merge workflow

- Add .github/workflows/dependabot-auto-merge.yml for auto-merge workflow
- Configure auto-merge for patch and minor updates only

Co-authored-by: Copilot <copilot@github.com>"

git push origin HEAD:main

echo "✓ Configuration files committed and pushed!"
