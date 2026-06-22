#:property PublishAot=false
#:property NoWarn=CA1416;CA2266
#:package Docnet.Core@2.6.0
#:package System.Drawing.Common@10.0.9
#:include PdfScriptJsonSupport.cs
#:include PdfScriptRenderingSupport.cs

using PdfScripts;
using System.Drawing;

var pageNumber = 0;
if (args.Length != 4 || !int.TryParse(args[0], out pageNumber))
{
    Console.WriteLine("Usage: dotnet run --file scripts\\create_validation_image.cs -- [page number] [fields.json file] [input image path] [output image path]");
    Environment.Exit(1);
}

var fieldsDocument = JsonUtilities.LoadFromFile<FieldsDocument>(args[1]);
using var image = PdfRenderingUtilities.LoadImageFromFile(args[2]);

var rectangles = new List<(double[] Rect, Color Color)>();
var boxCount = 0;
foreach (var field in fieldsDocument.FormFields.Where(x => x.PageNumber == pageNumber))
{
    rectangles.Add((field.EntryBoundingBox, Color.Red));
    rectangles.Add((field.LabelBoundingBox, Color.Blue));
    boxCount += 2;
}

PdfRenderingUtilities.DrawBoundingBoxes(image, rectangles);
image.Save(args[3]);
Console.WriteLine($"Created validation image at {args[3]} with {boxCount} bounding boxes");
