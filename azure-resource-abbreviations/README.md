# Azure Resource Abbreviations Skill

A skill for defining, verifying, and applying Azure resource naming conventions with the Microsoft Cloud Adoption Framework and the bundled `references\abbreviations.json` catalog.

## Overview

- The authoritative local reference is `references\abbreviations.json`.
- Utility scripts are implemented as **C# file-based apps**.
- The scripts use `#:include`, so they require **.NET SDK 10.0.300 or later**.

## When to Use This Skill

Use the `azure-resource-abbreviations` skill whenever you need to:

- define naming standards for Azure resources
- validate resource abbreviations or prefixes
- generate naming templates for infrastructure
- look up official Azure resource prefixes while writing IaC or automation

## Quick Start

### List available categories

```bash
dotnet run --file scripts\list_categories.cs
```

### Look up a resource abbreviation

```bash
dotnet run --file scripts\lookup_abbreviation.cs -- "Virtual Machine"
dotnet run --file scripts\lookup_abbreviation.cs -- "Web App"
dotnet run --file scripts\lookup_abbreviation.cs -- "Storage Account"
```

### Get all resources in a category

```bash
dotnet run --file scripts\get_category.cs -- "Networking"
dotnet run --file scripts\get_category.cs -- "Compute and Web"
dotnet run --file scripts\get_category.cs -- "Databases"
```

### Generate a naming template

```bash
dotnet run --file scripts\generate_template.cs -- "Virtual Machine"
```

## Derived Resource Categories

The JSON-backed scripts currently group resources into these categories:

1. **AI + Machine Learning**
2. **Analytics and IoT**
3. **Compute and Web**
4. **Containers**
5. **Databases**
6. **Developer Tools**
7. **Integration**
8. **Management and Governance**
9. **Migration**
10. **Networking**
11. **Security**
12. **Storage**

## Example Usage

### Scenario 1: Creating naming standards

```bash
dotnet run --file scripts\get_category.cs -- "Networking"
dotnet run --file scripts\get_category.cs -- "Compute and Web"
```

### Scenario 2: Validating a resource prefix

```bash
dotnet run --file scripts\lookup_abbreviation.cs -- "Virtual Machine"
```

### Scenario 3: Building IaC templates

Use the generated naming token in Terraform or Bicep:

```bicep
param location string
param environment string
param abbreviation string = 'vm'

var resourceName = '${abbreviation}-${environment}-${uniqueString(resourceGroup().id)}'
```

## File Structure

```text
azure-resource-abbreviations/
|-- SKILL.md
|-- README.md
|-- scripts/
|   |-- AzureResourceAbbreviationsSupport.cs
|   |-- lookup_abbreviation.cs
|   |-- list_categories.cs
|   |-- get_category.cs
|   `-- generate_template.cs
`-- references/
    |-- abbreviations.json
    `-- NAMING_GUIDE.md
```

## Script Notes

`AzureResourceAbbreviationsSupport.cs` loads `references\abbreviations.json`, derives display names and categories, and trims any trailing `-` when a prefix must be embedded inside a larger naming template.

## Examples

### Example 1: Virtual Machine naming

```bash
$ dotnet run --file scripts\generate_template.cs -- "Virtual Machine"
Display name:    Virtual Machine
Resource key:    computeVirtualMachines
Category:        Compute and Web
Official prefix: vm
Naming token:    vm

[STANDARD]
Template:     {abbr}-{env}-{instance}-{region}
Example:      vm-prod-web01-eus
```

### Example 2: Storage Account lookup

```bash
$ dotnet run --file scripts\lookup_abbreviation.cs -- "Storage Account"
Display name: Storage Account
Resource key:  storageStorageAccounts
Category:      Storage
Prefix:        st
```

### Example 3: Networking category

```bash
$ dotnet run --file scripts\get_category.cs -- "Networking"
[Networking]

Display name                       Resource key                                 Prefix
--------------------------------------------------------------------------------------------
Application Gateway               networkApplicationGateways                   agw-
Application Security Group        networkApplicationSecurityGroups             asg-
CDN Profile                       cdnProfiles                                  cdnp-
...
```

## References

- Local catalog: [references/abbreviations.json](references/abbreviations.json)
- Guidance: [references/NAMING_GUIDE.md](references/NAMING_GUIDE.md)
- Microsoft CAF resource abbreviations: <https://learn.microsoft.com/ja-jp/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations>
- Azure Naming Tool: <https://github.com/mspnp/AzureNamingTool>
