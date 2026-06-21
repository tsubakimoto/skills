---
name: azure-resource-abbreviations
description: "Use this skill whenever working with Azure resources and you need to define, verify, or understand resource naming conventions and abbreviations. This includes: creating resource naming standards, validating resource names, generating abbreviated names for different resource types, understanding Azure naming best practices, or designing naming schemes for cloud infrastructure. Use this skill whenever someone mentions Azure naming conventions, resource abbreviations, naming standards, or needs to follow Microsoft's Cloud Adoption Framework naming guidelines."
license: Proprietary. LICENSE has complete terms.
---

# Azure Resource Abbreviations Skill

## Skill directory

`~/.copilot/skills/azure-resource-abbreviations/`

## Quick Reference

| Task | Command |
|------|---------|
| Look up abbreviation | `dotnet run --file scripts\lookup_abbreviation.cs -- <resource_type>` |
| List all categories | `dotnet run --file scripts\list_categories.cs` |
| Get category resources | `dotnet run --file scripts\get_category.cs -- <category>` |
| Generate naming template | `dotnet run --file scripts\generate_template.cs -- <resource_type>` |

---

## Overview

This skill provides standardized Azure resource naming conventions and abbreviations as defined in the [Microsoft Cloud Adoption Framework](https://learn.microsoft.com/ja-jp/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations).

Azure resources should follow consistent naming conventions to ensure:
- **Readability**: Easily identify resource types and purposes
- **Organization**: Maintain consistent taxonomy across cloud infrastructure
- **Compliance**: Align with Microsoft's best practices
- **Automation**: Enable script-based resource management

---

## Resource Categories

The JSON-backed scripts surface the following categories:

1. **AI + Machine Learning** - Cognitive services, ML workspaces, AI search
2. **Analytics and IoT** - Data services, event processing, IoT resources
3. **Compute and Web** - VMs, App Services, Functions, cloud services
4. **Containers** - AKS, Container Registry, Container Instances
5. **Databases** - SQL, Cosmos DB, MySQL, PostgreSQL
6. **Developer Tools** - App Configuration, SignalR
7. **Integration** - API Management, Logic Apps, Service Bus
8. **Management and Governance** - Automation, Monitoring, dashboards, policy
9. **Migration** - Migration projects, DMS, Recovery Services
10. **Networking** - VNets, Load Balancers, gateways, firewalls
11. **Security** - Key Vault, Managed Identity, Bastion, VPN
12. **Storage** - Storage Accounts and related services

---

## Naming Guidelines

### Convention Structure

```
[resource-type][environment][instance][region]
```

**Example:**
- Resource: Virtual Machine → `vm`
- Environment: Production → `prod`
- Instance: Web Server 01 → `web01`
- Region: East US → `eus`

**Result:** `vm-prod-web01-eus`

### Best Practices

1. **Use consistent abbreviations** - Always use Microsoft's standard abbreviations
2. **Include environment designator** - dev, test, staging, prod
3. **Keep names concise** - Respect character limits (varies by resource type)
4. **Use lowercase** - Most Azure resources are case-insensitive, maintain readability
5. **Separate segments** - Use hyphens (-) or underscores (_) for clarity
6. **Avoid special characters** - Stick to alphanumeric and hyphens/underscores
7. **Make names descriptive** - Balance brevity with clarity

---

## Common Resource Abbreviations

### Compute and Web
| Resource | Abbreviation |
|----------|-------------|
| Virtual Machine | vm |
| VM Scale Set | vmss |
| App Service Plan | asp |
| Web App | app |
| Function App | func |
| Availability Set | avail |

### Networking
| Resource | Abbreviation |
|----------|-------------|
| Virtual Network | vnet |
| Subnet | snet |
| Network Interface | nic |
| Network Security Group | nsg |
| Load Balancer | lb |
| Public IP | pip |
| Application Gateway | agw |
| VPN Gateway | vpng |

### Storage and Databases
| Resource | Abbreviation |
|----------|-------------|
| Storage Account | st |
| SQL Server | sql |
| SQL Database | sqldb |
| Cosmos DB | cosmos |
| Key Vault | kv |
| Log Analytics | log |

### Containers
| Resource | Abbreviation |
|----------|-------------|
| AKS Cluster | aks |
| Container Registry | cr |
| Container Instance | ci |
| Container App | ca |

---

## Reference Documentation

For the complete list of Azure resource abbreviations and naming conventions, see:
- `azure-resource-abbreviations\references\abbreviations.json`
- [Microsoft Cloud Adoption Framework - Resource Abbreviations](https://learn.microsoft.com/ja-jp/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations)
- [Azure Naming Tool](https://github.com/mspnp/AzureNamingTool)

---

## C# file-based app scripts

This skill includes utility scripts implemented as C# file-based apps. They read `azure-resource-abbreviations\references\abbreviations.json` as the authoritative prefix catalog. Because these scripts use `#:include`, they require .NET SDK 10.0.300 or later.

### `lookup_abbreviation.cs`
Look up the standard abbreviation for a specific Azure resource type.

```bash
dotnet run --file scripts\lookup_abbreviation.cs -- "Virtual Machine"
```

### `list_categories.cs`
Display all available resource categories.

```bash
dotnet run --file scripts\list_categories.cs
```

### `get_category.cs`
List all resources in a specific category with their abbreviations.

```bash
dotnet run --file scripts\get_category.cs -- "Networking"
```

### `generate_template.cs`
Generate a naming template for a specific resource type.

```bash
dotnet run --file scripts\generate_template.cs -- "Web App"
```

---

## Use Cases

### 1. Creating Naming Standards
Use this skill to establish consistent naming conventions for your organization's Azure infrastructure.

### 2. Resource Validation
Verify that resource names follow the standard abbreviation conventions.

### 3. Naming Documentation
Generate documentation for your team about approved naming patterns.

### 4. Automation Scripts
Build infrastructure-as-code scripts that generate compliant resource names automatically.

### 5. Migration Planning
Ensure renamed resources align with modern Azure naming best practices during cloud migrations.

---

## Integration with Azure Naming Tool

For advanced automation and validation, consider using the [Azure Naming Tool](https://github.com/mspnp/AzureNamingTool), which provides:
- Standardized naming conventions
- Validation rules
- API for programmatic naming
- Multi-cloud support
