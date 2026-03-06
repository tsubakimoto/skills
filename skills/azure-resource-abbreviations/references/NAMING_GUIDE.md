# Azure Resource Abbreviations - Reference Guide

## Official References

- **Main Reference**: [Microsoft Cloud Adoption Framework - Resource Abbreviations](https://learn.microsoft.com/ja-jp/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations)
- **Naming Tool**: [Azure Naming Tool on GitHub](https://github.com/mspnp/AzureNamingTool)
- **Tagging Strategy**: [Resource Tagging Strategy](https://learn.microsoft.com/ja-jp/azure/cloud-adoption-framework/ready/azure-best-practices/resource-tagging)

## Quick Reference

### Resource Category Overview

| Category | Example Resources | Common Abbreviations |
|----------|-------------------|----------------------|
| **Compute** | VMs, App Services, Functions | vm, asp, func |
| **Networking** | VNets, NSGs, Load Balancers | vnet, nsg, lbi/lbe |
| **Storage** | Storage Accounts, Backup | st, bvault, sss |
| **Databases** | SQL, Cosmos DB, MySQL | sql, sqldb, cosmos, mysql |
| **Security** | Key Vault, Managed ID | kv, id, bas |
| **Containers** | AKS, Container Registry | aks, cr, ci, ca |
| **Analytics** | Data Factory, Synapse | adf, synw, dec |
| **Integration** | Logic Apps, API Management | logic, apim, sbns |

## Naming Convention Guidelines

### Pattern Structure

```
[prefix]-[abbreviation]-[environment]-[instance]-[region][-suffix]
```

Example: `myorg-vm-prod-web01-eus-001`

### Components

- **Prefix**: Organization or project prefix
- **Abbreviation**: Standardized resource type abbreviation (from this skill)
- **Environment**: dev, test, stg, prod, etc.
- **Instance**: Sequential or descriptive identifier
- **Region**: eus, wus, neu, weu, etc.
- **Suffix**: Optional (version, tier, etc.)

### Character Constraints

Different resource types have different character limits. Common limits:
- **Storage Accounts**: 3-24 characters, lowercase alphanumeric only
- **Virtual Machines**: 1-64 characters
- **App Services**: 1-60 characters
- **Key Vaults**: 3-24 characters

## Environment Designators

| Code | Meaning | Usage |
|------|---------|-------|
| dev | Development | Local development |
| test | Testing | QA and testing |
| stg | Staging | Pre-production |
| prod | Production | Live environment |
| dr | Disaster Recovery | DR/backup resources |

## Azure Region Abbreviations

| Abbreviation | Region | Location |
|--------------|--------|----------|
| eus | East US | Virginia, USA |
| wus | West US | California, USA |
| cus | Central US | Iowa, USA |
| neu | North Europe | Ireland |
| weu | West Europe | Netherlands |
| uksouth | UK South | London, UK |
| sea | Southeast Asia | Singapore |
| eas | East Asia | Hong Kong |
| jpeast | Japan East | Tokyo, Japan |
| auseast | Australia East | Sydney, Australia |

## Common Naming Patterns

### Virtual Machine
```
vm-[purpose]-[env]-[num]-[region]
Example: vm-web-prod-01-eus
```

### Web Application
```
app-[name]-[env]-[region]
Example: app-mysite-prod-eus
```

### Storage Account
```
st[org][purpose][env][random]
Example: stmyorgdata001
(Note: No hyphens, lowercase alphanumeric)
```

### Key Vault
```
kv-[org]-[purpose]-[env]
Example: kv-myorg-secrets-prod
```

### Network Resources
```
[type]-[org]-[env]-[region]
Examples:
  vnet-myorg-prod-eus
  nsg-myorg-prod-eus
  pip-myorg-web01-eus
```

## Implementation Tips

### 1. Document Your Standards
Create an organization-wide naming standard document that specifies:
- Abbreviations to use
- Environment codes
- Naming pattern
- Character limits
- Examples for each resource type

### 2. Use the Azure Naming Tool
Automate naming with the [Azure Naming Tool](https://github.com/mspnp/AzureNamingTool):
- Enforce naming standards
- Generate compliant names
- Validate resource names
- API for IaC integration

### 3. Infrastructure as Code
Include abbreviations in your IaC (Terraform, ARM, Bicep):
```bicep
param abbreviation string = 'vm'
param environment string = 'prod'
param instance int = 01

var resourceName = '${abbreviation}-${environment}-${instance}'
```

### 4. Validation Scripts
Create scripts to validate names:
```python
from azure_abbreviations import get_abbreviation, validate_naming_convention

# Validate a resource name
is_valid = validate_naming_convention("Virtual Machine", "vm")
```

## Common Mistakes to Avoid

- ❌ Using inconsistent abbreviations
- ❌ Using uppercase letters (causes issues with some resources)
- ❌ Using special characters other than hyphens/underscores
- ❌ Making names too long
- ❌ Missing environment or region designators
- ❌ Not documenting naming conventions

## Related Skills and Resources

- **Azure Best Practices**: Cloud Adoption Framework resources
- **Resource Tagging**: Complementary to naming for organization
- **Infrastructure as Code**: Automate resource creation with standards
- **Cost Management**: Combined with naming for better cost allocation

## FAQ

**Q: Can I customize abbreviations?**
A: You can, but it's best to stick with Microsoft's standards for consistency and team communication.

**Q: How do I name resources in cross-region deployments?**
A: Use region abbreviations in the name or tag resources with region information.

**Q: Should I include timestamps in names?**
A: Generally no. Use tags instead for metadata like creation date.

**Q: What about resources in multiple subscriptions?**
A: Include subscription or account identifiers if needed, but might be better handled with tags.
