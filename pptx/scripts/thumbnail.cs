#:property PublishAot=false
#:package DocumentFormat.OpenXml@3.2.0
#:package SixLabors.ImageSharp@3.1.11
#:package SixLabors.Fonts@2.0.3
#:package SixLabors.ImageSharp.Drawing@2.1.3
#:include ..\..\docx\scripts\office\OfficeSupport.cs
#:include PptxSupport.cs

using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using System.Globalization;

const int ThumbnailWidth = 300;
const int ConversionDpi = 100;
const int MaxCols = 6;
const int DefaultCols = 3;
const int JpegQuality = 95;
const int GridPadding = 20;
const int BorderWidth = 2;

if (args.Length is < 1 or > 3)
{
    Console.Error.WriteLine("Usage: dotnet run --file thumbnail.cs -- <input.pptx> [output-prefix] [--cols N]");
    Environment.Exit(2);
}

var input = args[0];
var outputPrefix = "thumbnails";
var cols = DefaultCols;
var index = 1;
if (index < args.Length && args[index] != "--cols")
{
    outputPrefix = args[index++];
}

if (index < args.Length)
{
    if (args[index] != "--cols" || index + 1 >= args.Length || !int.TryParse(args[index + 1], out cols))
    {
        Console.Error.WriteLine("Usage: dotnet run --file thumbnail.cs -- <input.pptx> [output-prefix] [--cols N]");
        Environment.Exit(2);
    }
}

cols = Math.Min(cols, MaxCols);
var inputPath = Path.GetFullPath(input);
if (!File.Exists(inputPath) || !string.Equals(Path.GetExtension(inputPath), ".pptx", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Error: Invalid PowerPoint file: {input}");
    Environment.Exit(1);
}

var outputPath = Path.GetFullPath($"{outputPrefix}.jpg");

try
{
    var slideInfo = PptxSupport.GetSlideInfo(inputPath);
    var tempDirectory = Path.Combine(Path.GetTempPath(), $"pptx-thumbnail-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDirectory);
    try
    {
        var visibleImages = ConvertToImages(inputPath, tempDirectory);
        if (visibleImages.Count == 0 && !slideInfo.Any(slide => slide.Hidden))
        {
            throw new InvalidOperationException("No slides found");
        }

        var slideList = BuildSlideList(slideInfo, visibleImages, tempDirectory);
        var files = CreateGrids(slideList, cols, outputPath);
        Console.WriteLine($"Created {files.Count} grid(s):");
        foreach (var file in files)
        {
            Console.WriteLine($"  {file}");
        }
    }
    finally
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}

static List<string> ConvertToImages(string pptxPath, string tempDirectory)
{
    var pdfExit = OfficeSupport.RunSoffice(
        ["--headless", "--convert-to", "pdf", "--outdir", tempDirectory, pptxPath],
        timeoutSeconds: 120);
    var pdfPath = Path.Combine(tempDirectory, $"{Path.GetFileNameWithoutExtension(pptxPath)}.pdf");
    if (pdfExit != 0 || !File.Exists(pdfPath))
    {
        throw new InvalidOperationException("PDF conversion failed");
    }

    var process = Process.Start(new ProcessStartInfo
    {
        FileName = "pdftoppm",
        UseShellExecute = false,
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        ArgumentList =
        {
            "-jpeg",
            "-r",
            ConversionDpi.ToString(CultureInfo.InvariantCulture),
            pdfPath,
            Path.Combine(tempDirectory, "slide")
        }
    }) ?? throw new InvalidOperationException("Failed to start pdftoppm.");
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException("Image conversion failed");
    }

    return Directory.GetFiles(tempDirectory, "slide-*.jpg").OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
}

static List<(string Path, string Label)> BuildSlideList(List<(string Name, bool Hidden)> slideInfo, List<string> visibleImages, string tempDirectory)
{
    var placeholderSize = visibleImages.Count > 0 ? GetImageSize(visibleImages[0]) : new Size(1920, 1080);
    var results = new List<(string Path, string Label)>();
    var visibleIndex = 0;
    foreach (var slide in slideInfo)
    {
        if (slide.Hidden)
        {
            var placeholderPath = Path.Combine(tempDirectory, $"hidden-{slide.Name}.jpg");
            using var image = CreateHiddenPlaceholder(placeholderSize);
            image.SaveAsJpeg(placeholderPath);
            results.Add((placeholderPath, $"{slide.Name} (hidden)"));
        }
        else if (visibleIndex < visibleImages.Count)
        {
            results.Add((visibleImages[visibleIndex++], slide.Name));
        }
    }

    return results;
}

static Size GetImageSize(string path)
{
    var image = Image.Identify(path);
    return image?.Size ?? new Size(1920, 1080);
}

static Image<Rgb24> CreateHiddenPlaceholder(Size size)
{
    var image = new Image<Rgb24>(size.Width, size.Height, Color.ParseHex("F0F0F0"));
    var lineWidth = Math.Max(5, Math.Min(size.Width, size.Height) / 100f);
    image.Mutate(context =>
    {
        context.DrawLine(Color.ParseHex("CCCCCC"), lineWidth, new PointF(0, 0), new PointF(size.Width, size.Height));
        context.DrawLine(Color.ParseHex("CCCCCC"), lineWidth, new PointF(size.Width, 0), new PointF(0, size.Height));
    });
    return image;
}

static List<string> CreateGrids(List<(string Path, string Label)> slides, int cols, string outputPath)
{
    var maxPerGrid = cols * (cols + 1);
    var files = new List<string>();
    for (var start = 0; start < slides.Count; start += maxPerGrid)
    {
        var chunk = slides.Skip(start).Take(maxPerGrid).ToList();
        using var grid = CreateGrid(chunk, cols, ThumbnailWidth);
        var filePath = slides.Count <= maxPerGrid
            ? outputPath
            : Path.Combine(Path.GetDirectoryName(outputPath)!, $"{Path.GetFileNameWithoutExtension(outputPath)}-{files.Count + 1}{Path.GetExtension(outputPath)}");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        grid.Save(filePath, new JpegEncoder { Quality = JpegQuality });
        files.Add(filePath);
    }

    return files;
}

static Image<Rgb24> CreateGrid(List<(string Path, string Label)> slides, int cols, int width)
{
    var fontSize = Math.Max(12, width / 10);
    var labelPadding = Math.Max(4, fontSize / 2);
    var font = SystemFonts.CreateFont("Arial", fontSize);

    using var probe = Image.Load<Rgb24>(slides[0].Path);
    var height = (int)(width * (probe.Height / (double)probe.Width));
    var rows = (slides.Count + cols - 1) / cols;
    var gridWidth = cols * width + (cols + 1) * GridPadding;
    var gridHeight = rows * (height + fontSize + labelPadding * 2) + (rows + 1) * GridPadding;
    var grid = new Image<Rgb24>(gridWidth, gridHeight, Color.White);

    for (var i = 0; i < slides.Count; i++)
    {
        var row = i / cols;
        var col = i % cols;
        var x = col * width + (col + 1) * GridPadding;
        var yBase = row * (height + fontSize + labelPadding * 2) + (row + 1) * GridPadding;
        var textSize = TextMeasurer.MeasureSize(slides[i].Label, new TextOptions(font));
        grid.Mutate(context => context.DrawText(slides[i].Label, font, Color.Black, new PointF(x + (width - textSize.Width) / 2f, yBase + labelPadding)));

        var yThumb = yBase + labelPadding + fontSize + labelPadding;
        using var image = Image.Load<Rgb24>(slides[i].Path);
        image.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(width, height),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3
        }));

        var thumbX = x + (width - image.Width) / 2;
        var thumbY = yThumb + (height - image.Height) / 2;
        grid.Mutate(context =>
        {
            context.DrawImage(image, new Point(thumbX, thumbY), 1f);
            context.Draw(Color.Gray, BorderWidth, new RectangleF(thumbX - BorderWidth, thumbY - BorderWidth, image.Width + BorderWidth * 2, image.Height + BorderWidth * 2));
        });
    }

    return grid;
}
