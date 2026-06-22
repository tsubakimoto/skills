#:property PublishAot=false
#:package DocumentFormat.OpenXml@3.2.0
#:include ..\..\..\docx\scripts\office\OfficeSupport.cs

var validate = true;
string? originalFile = null;
var values = new List<string>();
for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--original":
            originalFile = args[++index];
            break;
        case "--no-validate":
            validate = false;
            break;
        default:
            values.Add(args[index]);
            break;
    }
}

if (values.Count != 2)
{
    Console.Error.WriteLine("Usage: dotnet run --file pack.cs -- [--original <path>] [--no-validate] <input-dir> <output-file>");
    Environment.Exit(2);
}

var result = OfficeSupport.PackOffice(values[0], values[1], originalFile, validate, inferAuthor: null);
if (result.Success)
{
    Console.WriteLine(result.Message);
    return;
}

Console.Error.WriteLine(result.Message);
Environment.Exit(1);
