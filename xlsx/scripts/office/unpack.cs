#:property PublishAot=false
#:package DocumentFormat.OpenXml@3.2.0
#:include ..\..\..\docx\scripts\office\OfficeSupport.cs

var values = args.ToList();
if (values.Count != 2)
{
    Console.Error.WriteLine("Usage: dotnet run --file unpack.cs -- <input-file> <output-dir>");
    Environment.Exit(2);
}

var result = OfficeSupport.UnpackOffice(values[0], values[1], mergeRuns: false, simplifyRedlines: false);
if (result.Success)
{
    Console.WriteLine(result.Message);
    return;
}

Console.Error.WriteLine(result.Message);
Environment.Exit(1);
