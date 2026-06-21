#:property PublishAot=false
#:property NoWarn=CA2266
#:include AzureResourceAbbreviationsSupport.cs

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run --file scripts\\lookup_abbreviation.cs -- <resource_type>");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run --file scripts\\lookup_abbreviation.cs -- \"Virtual Machine\"");
    Console.WriteLine("  dotnet run --file scripts\\lookup_abbreviation.cs -- \"Storage Account\"");
    Console.WriteLine("  dotnet run --file scripts\\lookup_abbreviation.cs -- \"webSitesAppService\"");
    return 1;
}

var query = string.Join(' ', args);
var exactMatch = AzureResourceAbbreviationsSupport.FindExact(query);
if (exactMatch is not null)
{
    PrintEntry(exactMatch);
    return 0;
}

var results = AzureResourceAbbreviationsSupport.Search(query);
if (results.Count == 0)
{
    Console.WriteLine($"No abbreviation found for '{query}'.");
    Console.WriteLine($"Local catalog: {AzureResourceAbbreviationsSupport.GetReferencePath()}");
    return 1;
}

Console.WriteLine($"Found similar resources for '{query}':");
Console.WriteLine();

var groupedResults = results.GroupBy(entry => entry.Category, StringComparer.Ordinal);
foreach (var categoryGroup in groupedResults)
{
    Console.WriteLine($"[{categoryGroup.Key}]");
    foreach (var entry in categoryGroup)
    {
        PrintEntry(entry, "  ");
    }

    Console.WriteLine();
}

return 0;

static void PrintEntry(AzureResourceEntry entry, string indent = "")
{
    Console.WriteLine($"{indent}Display name:    {entry.DisplayName}");
    Console.WriteLine($"{indent}Resource key:    {entry.ResourceTypeKey}");
    Console.WriteLine($"{indent}Category:        {entry.Category}");
    Console.WriteLine($"{indent}Official prefix: {entry.OfficialPrefix}");
    Console.WriteLine($"{indent}Naming token:    {entry.NamingToken}");
}
