#:property PublishAot=false
#:package DocumentFormat.OpenXml@3.2.0
#:include OfficeSupport.cs

var verbose = false;
var autoRepair = false;
var author = "Claude";
var values = new List<string>();
for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--verbose":
            verbose = true;
            break;
        case "--auto-repair":
            autoRepair = true;
            break;
        case "--author":
            author = args[++index];
            break;
        default:
            values.Add(args[index]);
            break;
    }
}

if (values.Count is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: dotnet run --file validate.cs -- [--verbose] [--auto-repair] [--author <name>] <path> [original-file]");
    Environment.Exit(2);
}

var result = OfficeSupport.ValidateOffice(values[0], values.Count > 1 ? values[1] : null, verbose, autoRepair, author);
if (!string.IsNullOrWhiteSpace(result.Output))
{
    if (result.Success)
    {
        Console.WriteLine(result.Output);
    }
    else
    {
        Console.Error.WriteLine(result.Output);
    }
}

Environment.Exit(result.Success ? 0 : 1);
