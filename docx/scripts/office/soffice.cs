#:property PublishAot=false
#:package DocumentFormat.OpenXml@3.2.0
#:include OfficeSupport.cs

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: dotnet run --file soffice.cs -- <soffice args...>");
    Environment.Exit(2);
}

Environment.Exit(OfficeSupport.RunSoffice(args));
