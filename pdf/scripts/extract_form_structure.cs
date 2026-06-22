#:property PublishAot=false
#:property NoWarn=CA1416;CA2266
#:package PDFsharp@6.2.4
#:package PdfPig@0.1.14
#:package Docnet.Core@2.6.0
#:package System.Drawing.Common@10.0.9
#:include PdfScriptJsonSupport.cs
#:include PdfScriptRenderingSupport.cs
#:include PdfScriptFormSupport.cs

using PdfScripts;

if (args.Length != 2)
{
    Console.WriteLine("Usage: dotnet run --file scripts\\extract_form_structure.cs -- <input.pdf> <output.json>");
    Environment.Exit(1);
}

var pdfPath = args[0];
var outputPath = args[1];

Console.WriteLine($"Extracting structure from {pdfPath}...");
var structure = PdfFormUtilities.ExtractFormStructure(pdfPath);
JsonUtilities.SaveToFile(outputPath, structure);

Console.WriteLine("Found:");
Console.WriteLine($"  - {structure.Pages.Count} pages");
Console.WriteLine($"  - {structure.Labels.Count} text labels");
Console.WriteLine($"  - {structure.Lines.Count} horizontal lines");
Console.WriteLine($"  - {structure.Checkboxes.Count} checkboxes");
Console.WriteLine($"  - {structure.RowBoundaries.Count} row boundaries");
Console.WriteLine($"Saved to {outputPath}");
