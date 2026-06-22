#:property PublishAot=false
#:property NoWarn=CA1416;CA2266
#:package Docnet.Core@2.6.0
#:package System.Drawing.Common@10.0.9
#:include PdfScriptJsonSupport.cs
#:include PdfScriptRenderingSupport.cs

using PdfScripts;

if (args.Length != 2)
{
    Console.WriteLine("Usage: dotnet run --file scripts\\convert_pdf_to_images.cs -- [input pdf] [output directory]");
    Environment.Exit(1);
}

PdfRenderingUtilities.ConvertPdfToImages(args[0], args[1]);
