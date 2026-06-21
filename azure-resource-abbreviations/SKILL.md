---
name: azure-resource-abbreviations
description: >
  Use this skill whenever working with Azure resources and you need to define, verify, or
  understand resource naming conventions and abbreviations. This includes creating resource naming
  standards, validating Azure resource names, generating abbreviated names for resource types,
  checking naming templates for IaC, or following Microsoft's Cloud Adoption Framework guidance.
  Trigger even when the user only mentions Azure naming conventions, CAF naming, resource
  abbreviations, prefixes, or wants help naming Azure infrastructure consistently.
license: Proprietary. LICENSE has complete terms.
---

# Azure Resource Abbreviations Skill

This skill helps with **Azure resource abbreviations and naming conventions**. It uses the bundled
`references\abbreviations.json` catalog for fast local lookup and the Microsoft Cloud Adoption
Framework page as the authoritative fallback for ambiguous or newly added resources.

## Skill directory

`~/.copilot/skills/azure-resource-abbreviations/`

## Quick Reference

| Task | Command |
| --- | --- |
| Look up one resource | `dotnet run --file scripts\lookup_abbreviation.cs -- "<resource type>"` |
| List supported categories | `dotnet run --file scripts\list_categories.cs` |
| Show resources in a category | `dotnet run --file scripts\get_category.cs -- "<category>"` |
| Generate naming templates | `dotnet run --file scripts\generate_template.cs -- "<resource type>"` |

---

## Workflow

1. First determine whether the user needs **a single abbreviation**, **a shortlist of likely Azure
   resource matches**, **a category view**, or **a naming convention/template**.
2. Use the bundled C# file-based app scripts for deterministic local lookup:
   - `lookup_abbreviation.cs` for one resource type or fuzzy matching
   - `list_categories.cs` to browse available groups
   - `get_category.cs` to inspect all resources in a category
   - `generate_template.cs` when the user wants a reusable naming pattern
3. Treat `references\abbreviations.json` as the fast local catalog. If a query is ambiguous,
   missing, or the result will be used in governance/policy documentation, verify it against the
   Microsoft Learn page:
   `https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations`
4. When composing a full resource name, distinguish between:
   - **Official prefix**: the raw value from the bundled catalog, which can include a trailing `-`
   - **Naming token**: the prefix with a trailing `-` removed so it can be embedded in a larger
     naming template
5. Call out special cases instead of forcing a normal prefix pattern:
   - Some resources use descriptive names rather than a standard abbreviation
   - Some resources have stricter naming rules than others
   - Storage accounts and DNS-related resources often need extra rule checks beyond the prefix

---

## Script Requirements

The scripts in `scripts\` are implemented as **C# file-based apps** and use `#:include`.

- Require **.NET SDK 10.0.300 or later**
- Run them from this skill directory or pass the full path to the `.cs` file

---

## Output Expectations

When responding, prefer this structure when it fits:

| Field | Meaning |
| --- | --- |
| Resource | Human-readable Azure resource type |
| Provider | Azure resource provider / local resource key when relevant |
| Official prefix | Raw catalog value |
| Naming token | Prefix normalized for use inside a full name |
| Notes | Any ambiguity, special naming rules, or CAF verification note |

If there are multiple matches, do **not** guess. Return the shortlist and explain which one seems
closest.

---

## Resource Categories

The bundled catalog exposes these top-level categories:

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

---

## Examples

### Look up a resource abbreviation

```bash
dotnet run --file scripts\lookup_abbreviation.cs -- "Virtual Machine"
dotnet run --file scripts\lookup_abbreviation.cs -- "Storage Account"
dotnet run --file scripts\lookup_abbreviation.cs -- "Application Gateway"
```

### Explore a category

```bash
dotnet run --file scripts\list_categories.cs
dotnet run --file scripts\get_category.cs -- "Networking"
```

### Generate a naming template

```bash
dotnet run --file scripts\generate_template.cs -- "Web App"
```

Use the generated naming token in Bicep, Terraform, or documentation:

```bicep
param environment string
param region string

var namingToken = 'vm'
var resourceName = '${namingToken}-${environment}-web01-${region}'
```

---

## Guidance

- Prefer the **smallest exact answer** when the user only needs one abbreviation.
- Prefer the **category scripts** when the user is designing standards for many related resources.
- Prefer the **template script** when the user asks for a full naming convention rather than only a
  prefix.
- Use the Microsoft Learn page to verify final wording when the request is compliance-sensitive,
  governance-oriented, or the local catalog result looks outdated.
- Explain that Microsoft Learn is authoritative if it disagrees with the local catalog.

---

## References

- Local catalog: [references/abbreviations.json](./references/abbreviations.json)
- Reference guide: [references/NAMING_GUIDE.md](./references/NAMING_GUIDE.md)
- Microsoft Learn: <https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations>
