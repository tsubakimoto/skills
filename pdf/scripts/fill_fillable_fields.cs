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

if (args.Length != 3)
{
    Console.WriteLine("Usage: dotnet run --file scripts\\fill_fillable_fields.cs -- [input pdf] [field_values.json] [output pdf]");
    Environment.Exit(1);
}

PdfFormUtilities.FillFields(args[0], args[1], args[2]);
