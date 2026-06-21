#:property PublishAot=false
#:property NoWarn=CA2266
#:include AzureResourceAbbreviationsSupport.cs

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run --file scripts\\get_category.cs -- <category_name>");
    Console.WriteLine();
    Console.WriteLine("Available categories:");
    foreach (var category in AzureResourceAbbreviationsSupport.GetCategories())
    {
        Console.WriteLine($"  - {category}");
    }

    return 1;
}

var categoryName = string.Join(' ', args);
var entries = AzureResourceAbbreviationsSupport.GetCategoryEntries(categoryName);

if (entries.Count == 0)
{
    Console.WriteLine($"Category '{categoryName}' not found.");
    Console.WriteLine();
    Console.WriteLine("Available categories:");
    foreach (var category in AzureResourceAbbreviationsSupport.GetCategories())
    {
        Console.WriteLine($"  - {category}");
    }

    return 1;
}

Console.WriteLine($"[{entries[0].Category}]");
Console.WriteLine();
Console.WriteLine($"{"Display name",-34} {"Resource key",-44} {"Official",-12} {"Token",-12}");
Console.WriteLine(new string('-', 106));

foreach (var entry in entries)
{
    Console.WriteLine($"{entry.DisplayName,-34} {entry.ResourceTypeKey,-44} {entry.OfficialPrefix,-12} {entry.NamingToken,-12}");
}

Console.WriteLine();
Console.WriteLine($"Total: {entries.Count} resources");
return 0;
