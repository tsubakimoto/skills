# Azure Resource Naming Guide

Use this guide together with `abbreviations.json` when you need to turn a resource abbreviation into
an actual Azure naming pattern.

## Core idea

Use the resource abbreviation as one segment of a larger name rather than the whole name.

```text
<abbr>-<environment>-<workload>-<instance>-<region>
```

Example:

```text
vm-prod-api-01-eus
```

## Practical rules

1. Prefer Microsoft's published abbreviation when one exists.
2. Keep names short, predictable, and lowercase unless the resource type requires something else.
3. Add environment and workload context so names stay readable outside a single subscription.
4. Check resource-specific constraints before finalizing the pattern.

## Common environment codes

| Code | Meaning |
| --- | --- |
| `dev` | Development |
| `test` | Test / QA |
| `stg` | Staging |
| `prod` | Production |

## Common region codes

| Code | Region |
| --- | --- |
| `eus` | East US |
| `wus` | West US |
| `neu` | North Europe |
| `weu` | West Europe |
| `jpeast` | Japan East |

## Special cases

- **Storage accounts** often require lowercase alphanumeric names without separators.
- **DNS zones** and similar resources may need descriptive names rather than a short abbreviation.
- **Policy definitions** are usually better named descriptively than with a compact prefix.

## References

- <https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations>
- <https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/naming-and-tagging>
