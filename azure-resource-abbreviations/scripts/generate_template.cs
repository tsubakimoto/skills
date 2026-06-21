#:property PublishAot=false
#:property NoWarn=CA2266
#:include AzureResourceAbbreviationsSupport.cs

var namingTemplates = new Dictionary<string, (string Template, string Example, string Description)>(StringComparer.Ordinal)
{
    ["standard"] = ("{abbr}-{env}-{instance}-{region}", "{abbr}-prod-web01-eus", "Resource-Environment-Instance-Region"),
    ["simple"] = ("{abbr}{env}{instance}", "{abbr}prod1", "ResourceEnvironmentInstance (no separators)"),
    ["descriptive"] = ("{abbr}-{purpose}-{env}-{instance}", "{abbr}-webserver-prod-01", "Resource-Purpose-Environment-Instance")
};

var environmentCodes = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["dev"] = "Development",
    ["test"] = "Testing",
    ["stg"] = "Staging",
    ["prod"] = "Production"
};

var regionCodes = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["eus"] = "East US",
    ["wus"] = "West US",
    ["neu"] = "North Europe",
    ["weu"] = "West Europe",
    ["sea"] = "Southeast Asia",
    ["eas"] = "East Asia",
    ["jpeast"] = "Japan East"
};

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run --file scripts\\generate_template.cs -- <resource_type>");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run --file scripts\\generate_template.cs -- \"Virtual Machine\"");
    Console.WriteLine("  dotnet run --file scripts\\generate_template.cs -- webSitesAppService");
    return 1;
}

var query = string.Join(' ', args);
var entry = AzureResourceAbbreviationsSupport.FindExact(query);
if (entry is null)
{
    entry = AzureResourceAbbreviationsSupport.Search(query).FirstOrDefault();
}

if (entry is null)
{
    Console.WriteLine($"Resource type '{query}' not found.");
    Console.WriteLine($"Reference file: {AzureResourceAbbreviationsSupport.GetReferencePath()}");
    return 1;
}

Console.WriteLine($"Display name:    {entry.DisplayName}");
Console.WriteLine($"Resource key:    {entry.ResourceTypeKey}");
Console.WriteLine($"Category:        {entry.Category}");
Console.WriteLine($"Official prefix: {entry.OfficialPrefix}");
Console.WriteLine($"Naming token:    {entry.NamingToken}");
Console.WriteLine();
Console.WriteLine(new string('=', 70));
Console.WriteLine("NAMING TEMPLATES");
Console.WriteLine();

foreach (var (templateName, details) in namingTemplates)
{
    Console.WriteLine($"[{templateName.ToUpperInvariant()}]");
    Console.WriteLine($"Template:     {details.Template}");
    Console.WriteLine($"Example:      {details.Example.Replace("{abbr}", entry.NamingToken, StringComparison.Ordinal)}");
    Console.WriteLine($"Description:  {details.Description}");
    Console.WriteLine();
}

Console.WriteLine(new string('=', 70));
Console.WriteLine();
Console.WriteLine("NAMING COMPONENTS");
Console.WriteLine();
Console.WriteLine("Environment Codes:");
foreach (var (code, meaning) in environmentCodes)
{
    Console.WriteLine($"  {code,-6} = {meaning}");
}

Console.WriteLine();
Console.WriteLine("Common Region Codes:");
foreach (var (code, meaning) in regionCodes)
{
    Console.WriteLine($"  {code,-8} = {meaning}");
}

Console.WriteLine();
Console.WriteLine(new string('=', 70));
Console.WriteLine();
Console.WriteLine("BEST PRACTICES:");
Console.WriteLine("  - Use lowercase letters and numbers.");
Console.WriteLine("  - Use hyphens (-) as separators unless the resource type forbids them.");
Console.WriteLine("  - Keep names concise but descriptive.");
Console.WriteLine("  - Include environment and region for production resources.");
Console.WriteLine("  - Use the JSON reference as the authoritative source for prefixes.");
return 0;
