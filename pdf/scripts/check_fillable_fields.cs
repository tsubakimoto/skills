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

if (args.Length != 1)
{
    Console.WriteLine("Usage: dotnet run --file scripts\\check_fillable_fields.cs -- [input pdf]");
    Environment.Exit(1);
}

if (PdfFormUtilities.HasFillableFields(args[0]))
{
    Console.WriteLine("This PDF has fillable form fields");
}
else
{
    Console.WriteLine("This PDF does not have fillable form fields; you will need to visually determine where to enter data");
}
