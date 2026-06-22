#:property PublishAot=false
#:include PptxSupport.cs

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --file clean.cs -- <unpacked-dir>");
    Environment.Exit(2);
}

var unpackedDir = Path.GetFullPath(args[0]);
if (!Directory.Exists(unpackedDir))
{
    Console.Error.WriteLine($"Error: {args[0]} not found");
    Environment.Exit(1);
}

var removed = PptxSupport.CleanUnusedFiles(unpackedDir);
if (removed.Count == 0)
{
    Console.WriteLine("No unreferenced files found");
    return;
}

Console.WriteLine($"Removed {removed.Count} unreferenced files:");
foreach (var item in removed)
{
    Console.WriteLine($"  {item}");
}
