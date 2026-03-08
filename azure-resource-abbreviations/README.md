# Azure Resource Abbreviations Skill

A comprehensive skill for defining, verifying, and implementing Azure resource naming conventions and abbreviations based on Microsoft's Cloud Adoption Framework.

## Overview

This skill provides standardized Azure resource naming conventions and abbreviations as defined in the [Microsoft Cloud Adoption Framework](https://learn.microsoft.com/ja-jp/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations).

## When to Use This Skill

Use the azure-resource-abbreviations skill whenever you need to:

- **Define** standardized naming conventions for Azure resources
- **Verify** that resource names follow Microsoft's best practices
- **Generate** abbreviated names for different resource types
- **Understand** Azure naming conventions and guidelines
- **Design** naming schemes for cloud infrastructure
- **Create** infrastructure naming standards documentation
- **Automate** resource naming in scripts and IaC

### Trigger Phrases

- "Azure naming conventions"
- "Resource abbreviations"
- "Resource naming standards"
- "Azure resource names"
- "Naming scheme for Azure"
- "Azure best practices naming"
- "Resource naming policy"
- "Azure naming tool"

## Quick Start

### List Available Categories

```bash
python scripts/list_categories.py
```

### Look Up Resource Abbreviation

```bash
python scripts/lookup_abbreviation.py "Virtual Machine"
python scripts/lookup_abbreviation.py "Web App"
python scripts/lookup_abbreviation.py "Storage Account"
```

### Get Resources in a Category

```bash
python scripts/get_category.py "Networking"
python scripts/get_category.py "Compute and Web"
python scripts/get_category.py "Databases"
```

### Generate Naming Template

```bash
python scripts/generate_template.py "Virtual Machine"
```

## Supported Resource Categories

1. **AI + Machine Learning** - Cognitive services, ML workspaces, AI tools
2. **Analytics and IoT** - Data services, event processing, IoT resources
3. **Compute and Web** - VMs, App Services, Functions, cloud services
4. **Containers** - AKS, Container Registry, Container Instances
5. **Databases** - SQL, Cosmos DB, MySQL, PostgreSQL
6. **Developer Tools** - App Configuration, Maps, SignalR
7. **DevOps** - CI/CD, monitoring, automation
8. **Integration** - API Management, Logic Apps, Service Bus
9. **Management and Governance** - Automation, Monitoring, policies
10. **Migration** - Migration projects, DMS, Recovery Services
11. **Networking** - VNets, Load Balancers, gateways, firewalls
12. **Security** - Key Vault, Managed Identity, Bastion
13. **Storage** - Storage Accounts, Backup, File Shares
14. **Virtual Desktop Infrastructure** - AVD, host pools, workspaces

## Example Usage

### Scenario 1: Creating Naming Standards

```bash
# Get all network resources
python scripts/get_category.py "Networking"

# Get all compute resources
python scripts/get_category.py "Compute and Web"
```

### Scenario 2: Validating Resource Names

```python
from scripts.azure_abbreviations import get_abbreviation, validate_naming_convention

# Check if an abbreviation is correct
if validate_naming_convention("Virtual Machine", "vm"):
    print("Valid abbreviation!")
```

### Scenario 3: Building IaC Templates

Use abbreviations in your Terraform/Bicep templates:

```bicep
param location string
param environment string
param abbreviation string = 'vm'

var resourceName = '${abbreviation}-${environment}-${uniqueString(resourceGroup().id)}'

resource vm 'Microsoft.Compute/virtualMachines@2021-03-01' = {
  name: resourceName
  location: location
  // ... rest of configuration
}
```

## Common Resource Abbreviations

| Resource Type | Abbreviation | Category |
|---------------|--------------|----------|
| Virtual Machine | vm | Compute |
| Web App | app | Compute |
| App Service Plan | asp | Compute |
| Function App | func | Compute |
| Virtual Network | vnet | Networking |
| Subnet | snet | Networking |
| Network Security Group | nsg | Networking |
| Storage Account | st | Storage |
| SQL Server | sql | Databases |
| SQL Database | sqldb | Databases |
| Key Vault | kv | Security |
| AKS Cluster | aks | Containers |
| Container Registry | cr | Containers |

## File Structure

```
azure-resource-abbreviations/
├── SKILL.md                          # Skill definition and documentation
├── README.md                         # This file
├── scripts/
│   ├── __init__.py                  # Module initialization
│   ├── azure_abbreviations.py       # Core database and functions
│   ├── lookup_abbreviation.py       # Look up resource abbreviations
│   ├── list_categories.py           # List all categories
│   ├── get_category.py              # Get resources in a category
│   └── generate_template.py         # Generate naming templates
└── references/
    └── NAMING_GUIDE.md              # Comprehensive naming guide
```

## API Reference

### azure_abbreviations.py

```python
# Get abbreviation for a resource
abbreviation = get_abbreviation("Virtual Machine")  # Returns: "vm"

# Search for resources matching a query
results = search_abbreviation("sql")  # Returns: matching resources

# Get all resources in a category
resources = get_category_resources("Networking")  # Returns: dict

# Get all available categories
categories = get_all_categories()  # Returns: list

# Get all resources across all categories
all_resources = get_all_resources()  # Returns: dict of all resources

# Validate a naming convention
is_valid = validate_naming_convention("Virtual Machine", "vm")  # Returns: bool
```

## Best Practices

1. **Be Consistent** - Use the same abbreviations across your organization
2. **Document Standards** - Create a naming policy document for your team
3. **Include Context** - Add environment, region, and instance identifiers
4. **Respect Limits** - Check character limits for each resource type
5. **Use Lowercase** - Most Azure resources are case-insensitive; use lowercase for consistency
6. **Automate** - Use scripts or the Azure Naming Tool to enforce standards

## Integration

### With Infrastructure as Code

Integrate into your Terraform/Bicep templates:

```python
# In Python preprocessing
from scripts.azure_abbreviations import get_abbreviation

resource_abbr = get_abbreviation("Virtual Machine")
template = f"{resource_abbr}-prod-web-01-eus"
```

### With Azure Naming Tool

This skill complements the [Azure Naming Tool](https://github.com/mspnp/AzureNamingTool) for:
- Automated naming
- Policy enforcement
- API integration

### With CI/CD Pipelines

Validate resource names in your pipeline:

```bash
python scripts/lookup_abbreviation.py "Web App"
```

## Reference Documentation

- [Microsoft Cloud Adoption Framework - Resource Abbreviations](https://learn.microsoft.com/ja-jp/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations)
- [Resource Tagging Strategy](https://learn.microsoft.com/ja-jp/azure/cloud-adoption-framework/ready/azure-best-practices/resource-tagging)
- [Azure Naming Tool](https://github.com/mspnp/AzureNamingTool)

## Examples

### Example 1: Virtual Machine Naming

```bash
$ python scripts/generate_template.py "Virtual Machine"
Azure Resource: Virtual Machine
Abbreviation: vm

[STANDARD]
Template:     {abbr}-{env}-{instance}-{region}
Example:      vm-prod-web01-eus
Description:  Resource-Environment-Instance-Region

[SIMPLE]
Template:     {abbr}{env}{instance}
Example:      vmprod1
Description:  ResourceEnvironmentInstance (no separators)

[DESCRIPTIVE]
Template:     {abbr}-{purpose}-{env}-{instance}
Example:      vm-webserver-prod-01
Description:  Resource-Purpose-Environment-Instance
```

### Example 2: Storage Account Naming

```bash
$ python scripts/lookup_abbreviation.py "Storage Account"
✓ Storage Account: st
```

### Example 3: Get All Networking Resources

```bash
$ python scripts/get_category.py "Networking"
[Networking]

Resource                                            Abbreviation
-----------------------------------------------------------------
Application Gateway                                 agw
Application Security Group                          asg
CDN Profile                                         cdnp
...
```

## Support

For issues or questions about this skill, refer to:
- The [SKILL.md](SKILL.md) file for skill overview
- The [references/NAMING_GUIDE.md](references/NAMING_GUIDE.md) for detailed naming guidelines
- The official [Microsoft documentation](https://learn.microsoft.com/ja-jp/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations)

## License

This skill is provided as part of the Skills collection.

## Version

Version 1.0 - Based on Microsoft Cloud Adoption Framework (Last updated August 2025)
