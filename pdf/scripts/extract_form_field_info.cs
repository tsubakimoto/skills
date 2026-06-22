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
    Console.WriteLine("Usage: dotnet run --file scripts\\extract_form_field_info.cs -- [input pdf] [output json]");
    Environment.Exit(1);
}

PdfFormUtilities.WriteFieldInfo(args[0], args[1]);
