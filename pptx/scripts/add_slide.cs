#:property PublishAot=false
#:include PptxSupport.cs

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: dotnet run --file add_slide.cs -- <unpacked-dir> <source>");
    Environment.Exit(2);
}

var unpackedDir = Path.GetFullPath(args[0]);
var source = args[1];
if (!Directory.Exists(unpackedDir))
{
    Console.Error.WriteLine($"Error: {args[0]} not found");
    Environment.Exit(1);
}

try
{
    if (source.StartsWith("slideLayout", StringComparison.Ordinal) && source.EndsWith(".xml", StringComparison.Ordinal))
    {
        PptxSupport.CreateSlideFromLayout(unpackedDir, source);
    }
    else
    {
        PptxSupport.DuplicateSlide(unpackedDir, source);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.Exit(1);
}
