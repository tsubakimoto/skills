#:property PublishAot=false
#:package DocumentFormat.OpenXml@3.2.0
#:include ..\..\..\docx\scripts\office\OfficeSupport.cs

var values = args.ToList();
if (values.Count != 2)
{
    Console.Error.WriteLine("Usage: dotnet run --file pack.cs -- <input-dir> <output-file>");
    Environment.Exit(2);
}

var result = OfficeSupport.PackOffice(values[0], values[1], originalFile: null, validate: false, inferAuthor: null);
if (result.Success)
{
    Console.WriteLine(result.Message);
    return;
}

Console.Error.WriteLine(result.Message);
Environment.Exit(1);
