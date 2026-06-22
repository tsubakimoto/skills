using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Docnet.Core;
using Docnet.Core.Models;

namespace PdfScripts;

internal static class PdfRenderingUtilities
{
    public static void ConvertPdfToImages(string pdfPath, string outputDirectory, int maxDim = 1000)
    {
        Directory.CreateDirectory(outputDirectory);

        using var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(2.0));
        var pageCount = docReader.GetPageCount();

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            using var pageReader = docReader.GetPageReader(pageIndex);
            using var image = LoadImage(pageReader);

            var resized = ResizeIfNeeded(image, maxDim);
            var outputPath = Path.Combine(outputDirectory, $"page_{pageIndex + 1}.png");
            resized.Save(outputPath);
            Console.WriteLine($"Saved page {pageIndex + 1} as {outputPath} (size: ({resized.Width}, {resized.Height}))");
        }

        Console.WriteLine($"Converted {pageCount} pages to PNG images");
    }

    public static Bitmap LoadImageFromFile(string path)
        => new(path);

    public static void DrawBoundingBoxes(Bitmap image, IEnumerable<(double[] Rect, Color Color)> rectangles, int thickness = 2)
    {
        using var graphics = Graphics.FromImage(image);
        foreach (var (rect, color) in rectangles)
        {
            if (rect.Length != 4)
            {
                continue;
            }

            var x0 = (int)Math.Round(Math.Min(rect[0], rect[2]));
            var y0 = (int)Math.Round(Math.Min(rect[1], rect[3]));
            var x1 = (int)Math.Round(Math.Max(rect[0], rect[2]));
            var y1 = (int)Math.Round(Math.Max(rect[1], rect[3]));

            using var pen = new Pen(color, thickness);
            graphics.DrawRectangle(pen, x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }
    }

    public static IReadOnlyList<StructureLine> DetectHorizontalLines(Bitmap image, int pageNumber, double pdfWidth, double pdfHeight)
    {
        var mask = BinaryMask.FromImage(image);
        var candidates = new List<LineCandidate>();
        var minimumRun = (int)Math.Round(mask.Width * 0.5);

        for (var y = 0; y < mask.Height; y++)
        {
            var (start, end, darkCount) = FindLongestDarkRun(mask, y);
            if (start < 0)
            {
                continue;
            }

            var runLength = end - start + 1;
            if (runLength < minimumRun)
            {
                continue;
            }

            var density = darkCount / (double)runLength;
            if (density < 0.7)
            {
                continue;
            }

            candidates.Add(new LineCandidate(y, start, end));
        }

        var merged = MergeLineCandidates(candidates);
        return merged
            .Select(line => new StructureLine
            {
                Page = pageNumber,
                Y = Round1(line.CenterY * pdfHeight / mask.Height),
                X0 = Round1(line.MinX * pdfWidth / mask.Width),
                X1 = Round1(line.MaxX * pdfWidth / mask.Width)
            })
            .ToList();
    }

    public static IReadOnlyList<StructureCheckbox> DetectCheckboxes(Bitmap image, int pageNumber, double pdfWidth, double pdfHeight)
    {
        var mask = BinaryMask.FromImage(image);
        var visited = new bool[mask.Width * mask.Height];
        var checkboxes = new List<StructureCheckbox>();
        var queue = new Queue<int>();

        for (var y = 0; y < mask.Height; y++)
        {
            for (var x = 0; x < mask.Width; x++)
            {
                var index = y * mask.Width + x;
                if (!mask[index] || visited[index])
                {
                    continue;
                }

                visited[index] = true;
                queue.Enqueue(index);

                var minX = x;
                var maxX = x;
                var minY = y;
                var maxY = y;
                var pixelCount = 0;

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var currentX = current % mask.Width;
                    var currentY = current / mask.Width;
                    pixelCount++;

                    minX = Math.Min(minX, currentX);
                    maxX = Math.Max(maxX, currentX);
                    minY = Math.Min(minY, currentY);
                    maxY = Math.Max(maxY, currentY);

                    EnqueueIfDark(currentX - 1, currentY);
                    EnqueueIfDark(currentX + 1, currentY);
                    EnqueueIfDark(currentX, currentY - 1);
                    EnqueueIfDark(currentX, currentY + 1);
                }

                var width = maxX - minX + 1;
                var height = maxY - minY + 1;
                if (width < 6 || height < 6 || width > 64 || height > 64)
                {
                    continue;
                }

                var aspectDelta = Math.Abs(width - height);
                if (aspectDelta > Math.Max(2, Math.Min(width, height) * 0.25))
                {
                    continue;
                }

                var pdfWidthUnits = width * pdfWidth / mask.Width;
                var pdfHeightUnits = height * pdfHeight / mask.Height;
                if (pdfWidthUnits is < 5 or > 15 || pdfHeightUnits is < 5 or > 15)
                {
                    continue;
                }

                AnalyzeBox(mask, minX, minY, maxX, maxY, out var borderRatio, out var interiorRatio);
                if (borderRatio < 0.25 || interiorRatio > 0.35)
                {
                    continue;
                }

                if (pixelCount > width * height * 0.7)
                {
                    continue;
                }

                checkboxes.Add(new StructureCheckbox
                {
                    Page = pageNumber,
                    X0 = Round1(minX * pdfWidth / mask.Width),
                    Top = Round1(minY * pdfHeight / mask.Height),
                    X1 = Round1((maxX + 1) * pdfWidth / mask.Width),
                    Bottom = Round1((maxY + 1) * pdfHeight / mask.Height),
                    CenterX = Round1((minX + maxX + 1) * 0.5 * pdfWidth / mask.Width),
                    CenterY = Round1((minY + maxY + 1) * 0.5 * pdfHeight / mask.Height)
                });

                void EnqueueIfDark(int nextX, int nextY)
                {
                    if (nextX < 0 || nextX >= mask.Width || nextY < 0 || nextY >= mask.Height)
                    {
                        return;
                    }

                    var nextIndex = nextY * mask.Width + nextX;
                    if (!mask[nextIndex] || visited[nextIndex])
                    {
                        return;
                    }

                    visited[nextIndex] = true;
                    queue.Enqueue(nextIndex);
                }
            }
        }

        return checkboxes
            .OrderBy(x => x.Page)
            .ThenBy(x => x.Top)
            .ThenBy(x => x.X0)
            .ToList();
    }

    private static Bitmap LoadImage(Docnet.Core.Readers.IPageReader pageReader)
    {
        var width = pageReader.GetPageWidth();
        var height = pageReader.GetPageHeight();
        var bytes = pageReader.GetImage();
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(bytes, 0, data.Scan0, Math.Min(bytes.Length, Math.Abs(data.Stride) * height));
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private static Bitmap ResizeIfNeeded(Bitmap image, int maxDim)
    {
        if (image.Width <= maxDim && image.Height <= maxDim)
        {
            return new Bitmap(image);
        }

        var scale = Math.Min(maxDim / (double)image.Width, maxDim / (double)image.Height);
        var newWidth = Math.Max(1, (int)Math.Round(image.Width * scale));
        var newHeight = Math.Max(1, (int)Math.Round(image.Height * scale));
        var resized = new Bitmap(newWidth, newHeight);
        using var graphics = Graphics.FromImage(resized);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.DrawImage(image, 0, 0, newWidth, newHeight);
        return resized;
    }

    private static (int Start, int End, int DarkCount) FindLongestDarkRun(BinaryMask mask, int y)
    {
        var bestStart = -1;
        var bestEnd = -1;
        var bestDarkCount = 0;

        var currentStart = -1;
        var currentDarkCount = 0;
        var gapCount = 0;

        for (var x = 0; x < mask.Width; x++)
        {
            if (mask[x, y])
            {
                if (currentStart < 0)
                {
                    currentStart = x;
                }

                currentDarkCount++;
                gapCount = 0;
                continue;
            }

            if (currentStart < 0)
            {
                continue;
            }

            if (gapCount < 2)
            {
                gapCount++;
                continue;
            }

            var currentEnd = x - gapCount - 1;
            if (currentDarkCount > bestDarkCount)
            {
                bestStart = currentStart;
                bestEnd = currentEnd;
                bestDarkCount = currentDarkCount;
            }

            currentStart = -1;
            currentDarkCount = 0;
            gapCount = 0;
        }

        if (currentStart >= 0)
        {
            var currentEnd = mask.Width - gapCount - 1;
            if (currentDarkCount > bestDarkCount)
            {
                bestStart = currentStart;
                bestEnd = currentEnd;
                bestDarkCount = currentDarkCount;
            }
        }

        return (bestStart, bestEnd, bestDarkCount);
    }

    private static List<MergedLine> MergeLineCandidates(List<LineCandidate> candidates)
    {
        var result = new List<MergedLine>();
        foreach (var candidate in candidates.OrderBy(c => c.Y).ThenBy(c => c.StartX))
        {
            var existing = result.LastOrDefault();
            if (existing is not null &&
                Math.Abs(existing.MaxY - candidate.Y) <= 2 &&
                candidate.StartX <= existing.MaxX + 10 &&
                candidate.EndX >= existing.MinX - 10)
            {
                existing.MaxY = candidate.Y;
                existing.MinX = Math.Min(existing.MinX, candidate.StartX);
                existing.MaxX = Math.Max(existing.MaxX, candidate.EndX);
            }
            else
            {
                result.Add(new MergedLine
                {
                    MinY = candidate.Y,
                    MaxY = candidate.Y,
                    MinX = candidate.StartX,
                    MaxX = candidate.EndX
                });
            }
        }

        return result;
    }

    private static void AnalyzeBox(BinaryMask mask, int minX, int minY, int maxX, int maxY, out double borderRatio, out double interiorRatio)
    {
        var borderPixels = 0;
        var borderDark = 0;
        var interiorPixels = 0;
        var interiorDark = 0;

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var isBorder = x == minX || x == maxX || y == minY || y == maxY;
                if (isBorder)
                {
                    borderPixels++;
                    if (mask[x, y])
                    {
                        borderDark++;
                    }
                }
                else
                {
                    interiorPixels++;
                    if (mask[x, y])
                    {
                        interiorDark++;
                    }
                }
            }
        }

        borderRatio = borderPixels == 0 ? 0 : borderDark / (double)borderPixels;
        interiorRatio = interiorPixels == 0 ? 0 : interiorDark / (double)interiorPixels;
    }

    private static double Round1(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    private sealed class BinaryMask
    {
        private readonly bool[] _data;

        public int Width { get; }

        public int Height { get; }

        public bool this[int index] => _data[index];

        public bool this[int x, int y] => _data[y * Width + x];

        private BinaryMask(int width, int height, bool[] data)
        {
            Width = width;
            Height = height;
            _data = data;
        }

        public static BinaryMask FromImage(Bitmap image)
        {
            var data = new bool[image.Width * image.Height];
            var rect = new Rectangle(0, 0, image.Width, image.Height);
            var bitmapData = image.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var stride = Math.Abs(bitmapData.Stride);
                var bytes = new byte[stride * image.Height];
                Marshal.Copy(bitmapData.Scan0, bytes, 0, bytes.Length);

                for (var y = 0; y < image.Height; y++)
                {
                    var rowOffset = y * stride;
                    for (var x = 0; x < image.Width; x++)
                    {
                        var pixelOffset = rowOffset + (x * 4);
                        var b = bytes[pixelOffset];
                        var g = bytes[pixelOffset + 1];
                        var r = bytes[pixelOffset + 2];
                        var a = bytes[pixelOffset + 3];
                        var luminance = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
                        data[y * image.Width + x] = a > 0 && luminance < 180;
                    }
                }
            }
            finally
            {
                image.UnlockBits(bitmapData);
            }

            return new BinaryMask(image.Width, image.Height, data);
        }
    }

    private sealed record LineCandidate(int Y, int StartX, int EndX);

    private sealed class MergedLine
    {
        public int MinY { get; set; }

        public int MaxY { get; set; }

        public int MinX { get; set; }

        public int MaxX { get; set; }

        public double CenterY => (MinY + MaxY) / 2.0;
    }
}
