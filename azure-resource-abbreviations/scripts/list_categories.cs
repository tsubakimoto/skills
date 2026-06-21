#:property PublishAot=false
#:property NoWarn=CA2266
#:include AzureResourceAbbreviationsSupport.cs

var categories = AzureResourceAbbreviationsSupport.GetCategories();

Console.WriteLine("Available Azure resource categories:");
Console.WriteLine();

for (var index = 0; index < categories.Count; index++)
{
    Console.WriteLine($"{index + 1,2}. {categories[index]}");
}

Console.WriteLine();
Console.WriteLine($"Total: {categories.Count} categories");
Console.WriteLine("Use 'dotnet run --file scripts\\get_category.cs -- <category_name>' to list resources in one category.");
return 0;
