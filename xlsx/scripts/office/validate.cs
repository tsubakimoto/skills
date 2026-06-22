#:property PublishAot=false
#:package DocumentFormat.OpenXml@3.2.0
#:include ..\..\..\docx\scripts\office\OfficeSupport.cs

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --file validate.cs -- <path>");
    Environment.Exit(2);
}

var result = OfficeSupport.ValidateOffice(args[0], originalFile: null, verbose: false, autoRepair: false, author: "Claude");
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
