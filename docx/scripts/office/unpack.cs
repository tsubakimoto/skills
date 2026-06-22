#:property PublishAot=false
#:package DocumentFormat.OpenXml@3.2.0
#:include OfficeSupport.cs

var mergeRuns = true;
var simplifyRedlines = true;
var values = new List<string>();
for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--no-merge-runs":
            mergeRuns = false;
            break;
        case "--no-simplify-redlines":
            simplifyRedlines = false;
            break;
        default:
            values.Add(args[index]);
            break;
    }
}

if (values.Count != 2)
{
    Console.Error.WriteLine("Usage: dotnet run --file unpack.cs -- [--no-merge-runs] [--no-simplify-redlines] <input-file> <output-dir>");
    Environment.Exit(2);
}

var result = OfficeSupport.UnpackOffice(values[0], values[1], mergeRuns, simplifyRedlines);
if (result.Success)
{
    Console.WriteLine(result.Message);
    return;
}

Console.Error.WriteLine(result.Message);
Environment.Exit(1);
