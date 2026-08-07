# Dependabot auto-merge setup script
# Usage: .\setup.ps1 -Repo owner/repo [-CiChecks "build", "test"]

param(
    [Parameter(Mandatory=$true)]
    [string]$Repo,
    
    [Parameter(Mandatory=$false)]
    [string[]]$CiChecks = @()
)

# Check if gh CLI is installed
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Error: GitHub CLI (gh) is not installed." -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install it first:"
    Write-Host "  Windows: choco install gh"
    Write-Host "  Or download from: https://github.com/cli/cli/releases"
    Write-Host ""
    Write-Host "After installation, authenticate with: gh auth login"
    exit 1
}

# Check if git is installed
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Error: Git is not installed." -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install it first:"
    Write-Host "  Download from: https://git-scm.com/download/win"
    exit 1
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$AssetsDir = Join-Path (Split-Path -Parent $ScriptDir) "assets"

Write-Host "Setting up Dependabot auto-merge for: $Repo"

# Enable auto-merge
Write-Host "Enabling auto-merge..."
gh repo edit $Repo --enable-auto-merge

# Enable squash merge
Write-Host "Enabling squash merge..."
gh repo edit $Repo --enable-squash-merge

# Set default branch to main
Write-Host "Setting default branch to main..."
gh repo edit $Repo --default-branch main

# Create ruleset
Write-Host "Creating branch ruleset..."

$ruleset = @{
    name = "Main branch protection"
    target = "branch"
    enforcement = "active"
    conditions = @{
        ref_name = @{
            include = @("~DEFAULT_BRANCH")
            exclude = @()
        }
    }
    rules = @(
        @{ type = "deletion" },
        @{ type = "non_fast_forward" }
    )
}

# Add CI checks if provided
if ($CiChecks.Count -gt 0) {
    $statusChecks = @()
    foreach ($check in $CiChecks) {
        $statusChecks += @{
            context = $check
            integration_id = $null
        }
    }
    
    $ruleset.rules += @{
        type = "required_status_checks"
        parameters = @{
            required_status_checks = $statusChecks
            strict_required_status_checks_policy = $true
        }
    }
}

$rulesetJson = $ruleset | ConvertTo-Json -Depth 10
$rulesetJson | gh api repos/$Repo/rulesets -X POST --input -

Write-Host "✓ Repository configuration completed!"

# Clone repository and add configuration files
Write-Host ""
Write-Host "Cloning repository and adding configuration files..."

$TempDir = New-TemporaryDirectory
Push-Location $TempDir

try {
    gh repo clone $Repo .
    
    # Create .github directories if they don't exist
    New-Item -ItemType Directory -Path ".github\workflows" -Force | Out-Null
    
    # Copy configuration files
    Copy-Item "$AssetsDir\dependabot-auto-merge.yml" ".github\workflows\dependabot-auto-merge.yml"
    
    # Commit and push
    git config user.name "github-actions[bot]"
    git config user.email "github-actions[bot]@users.noreply.github.com"
    
    git add ".github/workflows/dependabot-auto-merge.yml"
    git commit -m "chore: add Dependabot auto-merge workflow

- Add .github/workflows/dependabot-auto-merge.yml for auto-merge workflow
- Configure auto-merge for patch and minor updates only

Co-authored-by: Copilot <copilot@github.com>"
    
    git push origin HEAD:main
    
    Write-Host "✓ Configuration files committed and pushed!"
}
finally {
    Pop-Location
    Remove-Item -Recurse -Force $TempDir
}
