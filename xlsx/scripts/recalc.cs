#:property PublishAot=false
#:package DocumentFormat.OpenXml@3.2.0
#:include ..\..\docx\scripts\office\OfficeSupport.cs
#:include XlsxSupport.cs

using System.Text.Json;

const string MacroXml = """
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE script:module PUBLIC "-//OpenOffice.org//DTD OfficeDocument 1.0//EN" "module.dtd">
<script:module xmlns:script="http://openoffice.org/2000/script" script:name="Module1" script:language="StarBasic">
    Sub RecalculateAndSave()
      ThisComponent.calculateAll()
      ThisComponent.store()
      ThisComponent.close(True)
    End Sub
</script:module>
""";

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: dotnet run --file recalc.cs -- <excel-file> [timeout-seconds]");
    Environment.Exit(2);
}

var filePath = Path.GetFullPath(args[0]);
var timeoutSeconds = args.Length == 2 && int.TryParse(args[1], out var parsedTimeout) ? parsedTimeout : 30;
if (!File.Exists(filePath))
{
    Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object> { ["error"] = $"File {args[0]} does not exist" }, new JsonSerializerOptions { WriteIndented = true }));
    return;
}

if (!SetupMacro())
{
    Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object> { ["error"] = "Failed to setup LibreOffice macro" }, new JsonSerializerOptions { WriteIndented = true }));
    return;
}

var exitCode = OfficeSupport.RunSoffice(
    ["--headless", "--norestore", "vnd.sun.star.script:Standard.Module1.RecalculateAndSave?language=Basic&location=application", filePath],
    timeoutSeconds);

if (exitCode is not 0 and not 124)
{
    Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object> { ["error"] = "Unknown error during recalculation" }, new JsonSerializerOptions { WriteIndented = true }));
    return;
}

var result = XlsxSupport.InspectWorkbook(filePath);
Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));

static bool SetupMacro()
{
    var macroDirectory = OperatingSystem.IsMacOS()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support", "LibreOffice", "4", "user", "basic", "Standard")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".config", "libreoffice", "4", "user", "basic", "Standard");
    var macroFile = Path.Combine(macroDirectory, "Module1.xba");
    if (File.Exists(macroFile) && File.ReadAllText(macroFile).Contains("RecalculateAndSave", StringComparison.Ordinal))
    {
        return true;
    }

    if (!Directory.Exists(macroDirectory))
    {
        _ = OfficeSupport.RunSoffice(["--headless", "--terminate_after_init"], timeoutSeconds: 10);
        Directory.CreateDirectory(macroDirectory);
    }

    try
    {
        File.WriteAllText(macroFile, MacroXml);
        return true;
    }
    catch
    {
        return false;
    }
}
