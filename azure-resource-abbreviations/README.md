# Azure Resource Abbreviations

Look up Azure resource abbreviations and naming prefixes with a local JSON catalog and C# file-based
app helpers.

## Files

```text
azure-resource-abbreviations/
|-- SKILL.md
|-- README.md
|-- references/
|   |-- abbreviations.json
|   `-- NAMING_GUIDE.md
`-- scripts/
    |-- AzureResourceAbbreviationsSupport.cs
    |-- generate_template.cs
    |-- get_category.cs
    |-- list_categories.cs
    `-- lookup_abbreviation.cs
```

## Commands

```powershell
dotnet run --file scripts\lookup_abbreviation.cs -- "Virtual Machine"
dotnet run --file scripts\list_categories.cs
dotnet run --file scripts\get_category.cs -- "Networking"
dotnet run --file scripts\generate_template.cs -- "Storage Account"
```

## Sources

- `references\abbreviations.json`
- <https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations>
