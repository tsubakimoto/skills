#:property PublishAot=false
#:package DocumentFormat.OpenXml@3.2.0
#:include office\OfficeSupport.cs

const string LibreOfficeProfile = "/tmp/libreoffice_docx_profile";
const string MacroXml = """
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE script:module PUBLIC "-//OpenOffice.org//DTD OfficeDocument 1.0//EN" "module.dtd">
<script:module xmlns:script="http://openoffice.org/2000/script" script:name="Module1" script:language="StarBasic">
    Sub AcceptAllTrackedChanges()
        Dim document As Object
        Dim dispatcher As Object

        document = ThisComponent.CurrentController.Frame
        dispatcher = createUnoService("com.sun.star.frame.DispatchHelper")

        dispatcher.executeDispatch(document, ".uno:AcceptAllTrackedChanges", "", 0, Array())
        ThisComponent.store()
        ThisComponent.close(True)
    End Sub
</script:module>
""";

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: dotnet run --file accept_changes.cs -- <input-file> <output-file>");
    Environment.Exit(2);
}

var inputPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);
if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Error: Input file not found: {args[0]}");
    Environment.Exit(1);
}

if (!string.Equals(Path.GetExtension(inputPath), ".docx", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Error: Input file is not a DOCX file: {args[0]}");
    Environment.Exit(1);
}

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.Copy(inputPath, outputPath, overwrite: true);

if (!SetupMacro())
{
    Console.Error.WriteLine("Error: Failed to setup LibreOffice macro");
    Environment.Exit(1);
}

var exitCode = OfficeSupport.RunSoffice(
    [
        "--headless",
        $"-env:UserInstallation=file://{LibreOfficeProfile}",
        "--norestore",
        "vnd.sun.star.script:Standard.Module1.AcceptAllTrackedChanges?language=Basic&location=application",
        outputPath
    ],
    timeoutSeconds: 30);

if (exitCode is not 0 and not 124)
{
    Console.Error.WriteLine("Error: LibreOffice failed");
    Environment.Exit(1);
}

Console.WriteLine($"Successfully accepted all tracked changes: {args[0]} -> {args[1]}");

static bool SetupMacro()
{
    var macroDirectory = Path.Combine(LibreOfficeProfile, "user", "basic", "Standard");
    var macroFile = Path.Combine(macroDirectory, "Module1.xba");
    if (File.Exists(macroFile) && File.ReadAllText(macroFile).Contains("AcceptAllTrackedChanges", StringComparison.Ordinal))
    {
        return true;
    }

    if (!Directory.Exists(macroDirectory))
    {
        _ = OfficeSupport.RunSoffice(["--headless", $"-env:UserInstallation=file://{LibreOfficeProfile}", "--terminate_after_init"], timeoutSeconds: 10);
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
