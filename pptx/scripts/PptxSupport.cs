using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class PptxSupport
{
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PkgRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static int GetNextSlideNumber(string unpackedDir)
    {
        var slidesDirectory = Path.Combine(unpackedDir, "ppt", "slides");
        var numbers = Directory.Exists(slidesDirectory)
            ? Directory.GetFiles(slidesDirectory, "slide*.xml")
                .Select(path => Regex.Match(Path.GetFileName(path), @"^slide(\d+)\.xml$"))
                .Where(match => match.Success)
                .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
                .DefaultIfEmpty(0)
            : [0];
        return numbers.Max() + 1;
    }

    public static int GetNextSlideId(string unpackedDir)
    {
        var presentationPath = Path.Combine(unpackedDir, "ppt", "presentation.xml");
        var document = XDocument.Load(presentationPath);
        return document.Descendants(P + "sldId")
            .Select(element => int.TryParse((string?)element.Attribute("id"), out var id) ? id : 255)
            .DefaultIfEmpty(255)
            .Max() + 1;
    }

    public static string AddPresentationRelationship(string unpackedDir, string slideFileName)
    {
        var path = Path.Combine(unpackedDir, "ppt", "_rels", "presentation.xml.rels");
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("presentation.xml.rels is empty.");
        var existing = root.Elements(PkgRelationships + "Relationship")
            .FirstOrDefault(element => (string?)element.Attribute("Target") == $"slides/{slideFileName}");
        if (existing is not null)
        {
            return (string?)existing.Attribute("Id") ?? string.Empty;
        }

        var nextId = root.Elements(PkgRelationships + "Relationship")
            .Select(element => (string?)element.Attribute("Id"))
            .Where(value => value?.StartsWith("rId", StringComparison.Ordinal) == true)
            .Select(value => int.TryParse(value![3..], out var parsed) ? parsed : 0)
            .DefaultIfEmpty()
            .Max() + 1;
        var rid = $"rId{nextId}";
        root.Add(new XElement(PkgRelationships + "Relationship",
            new XAttribute("Id", rid),
            new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide"),
            new XAttribute("Target", $"slides/{slideFileName}")));
        document.Save(path, SaveOptions.DisableFormatting);
        return rid;
    }

    public static void EnsureSlideContentType(string unpackedDir, string slideFileName)
    {
        var path = Path.Combine(unpackedDir, "[Content_Types].xml");
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("[Content_Types].xml is empty.");
        if (root.Elements(ContentTypes + "Override").Any(element => (string?)element.Attribute("PartName") == $"/ppt/slides/{slideFileName}"))
        {
            return;
        }

        root.Add(new XElement(ContentTypes + "Override",
            new XAttribute("PartName", $"/ppt/slides/{slideFileName}"),
            new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.presentationml.slide+xml")));
        document.Save(path, SaveOptions.DisableFormatting);
    }

    public static void CreateSlideFromLayout(string unpackedDir, string layoutFile)
    {
        var layoutPath = Path.Combine(unpackedDir, "ppt", "slideLayouts", layoutFile);
        if (!File.Exists(layoutPath))
        {
            throw new FileNotFoundException($"Error: {layoutPath} not found");
        }

        var nextNumber = GetNextSlideNumber(unpackedDir);
        var slideFileName = $"slide{nextNumber}.xml";
        var slidesDirectory = Path.Combine(unpackedDir, "ppt", "slides");
        var relsDirectory = Path.Combine(slidesDirectory, "_rels");
        Directory.CreateDirectory(relsDirectory);

        File.WriteAllText(Path.Combine(slidesDirectory, slideFileName), """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:sld xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
  <p:cSld>
    <p:spTree>
      <p:nvGrpSpPr>
        <p:cNvPr id="1" name=""/>
        <p:cNvGrpSpPr/>
        <p:nvPr/>
      </p:nvGrpSpPr>
      <p:grpSpPr>
        <a:xfrm>
          <a:off x="0" y="0"/>
          <a:ext cx="0" cy="0"/>
          <a:chOff x="0" y="0"/>
          <a:chExt cx="0" cy="0"/>
        </a:xfrm>
      </p:grpSpPr>
    </p:spTree>
  </p:cSld>
  <p:clrMapOvr>
    <a:masterClrMapping/>
  </p:clrMapOvr>
</p:sld>
""");

        File.WriteAllText(Path.Combine(relsDirectory, $"{slideFileName}.rels"), $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/{layoutFile}"/>
</Relationships>
""");

        EnsureSlideContentType(unpackedDir, slideFileName);
        var rid = AddPresentationRelationship(unpackedDir, slideFileName);
        var slideId = GetNextSlideId(unpackedDir);
        Console.WriteLine($"Created {slideFileName} from {layoutFile}");
        Console.WriteLine($"Add to presentation.xml <p:sldIdLst>: <p:sldId id=\"{slideId}\" r:id=\"{rid}\"/>");
    }

    public static void DuplicateSlide(string unpackedDir, string sourceFile)
    {
        var sourceSlidePath = Path.Combine(unpackedDir, "ppt", "slides", sourceFile);
        if (!File.Exists(sourceSlidePath))
        {
            throw new FileNotFoundException($"Error: {sourceSlidePath} not found");
        }

        var nextNumber = GetNextSlideNumber(unpackedDir);
        var slideFileName = $"slide{nextNumber}.xml";
        var slidesDirectory = Path.Combine(unpackedDir, "ppt", "slides");
        var relsDirectory = Path.Combine(slidesDirectory, "_rels");
        Directory.CreateDirectory(relsDirectory);

        File.Copy(sourceSlidePath, Path.Combine(slidesDirectory, slideFileName), overwrite: true);
        var sourceRels = Path.Combine(relsDirectory, $"{sourceFile}.rels");
        var destinationRels = Path.Combine(relsDirectory, $"{slideFileName}.rels");
        if (File.Exists(sourceRels))
        {
            var document = XDocument.Load(sourceRels, LoadOptions.PreserveWhitespace);
            var toRemove = document.Root?.Elements(PkgRelationships + "Relationship")
                .Where(element => ((string?)element.Attribute("Type"))?.Contains("notesSlide", StringComparison.OrdinalIgnoreCase) == true)
                .ToList() ?? [];
            foreach (var item in toRemove)
            {
                item.Remove();
            }

            document.Save(destinationRels, SaveOptions.DisableFormatting);
        }

        EnsureSlideContentType(unpackedDir, slideFileName);
        var rid = AddPresentationRelationship(unpackedDir, slideFileName);
        var slideId = GetNextSlideId(unpackedDir);
        Console.WriteLine($"Created {slideFileName} from {sourceFile}");
        Console.WriteLine($"Add to presentation.xml <p:sldIdLst>: <p:sldId id=\"{slideId}\" r:id=\"{rid}\"/>");
    }

    public static List<string> CleanUnusedFiles(string unpackedDir)
    {
        var removed = new List<string>();
        removed.AddRange(RemoveOrphanedSlides(unpackedDir));
        removed.AddRange(RemoveTrash(unpackedDir));
        while (true)
        {
            var batch = RemoveOrphanedRelationshipFiles(unpackedDir);
            var referenced = GetReferencedFiles(unpackedDir);
            batch.AddRange(RemoveOrphanedFiles(unpackedDir, referenced));
            if (batch.Count == 0)
            {
                break;
            }

            removed.AddRange(batch);
        }

        if (removed.Count > 0)
        {
            UpdateContentTypes(unpackedDir, removed);
        }

        return removed;
    }

    public static List<(string Name, bool Hidden)> GetSlideInfo(string pptxFile)
    {
        using var archive = ZipFile.OpenRead(pptxFile);
        var rels = XDocument.Load(archive.GetEntry("ppt/_rels/presentation.xml.rels")!.Open());
        var ridToSlide = rels.Descendants(PkgRelationships + "Relationship")
            .Where(element =>
                ((string?)element.Attribute("Type"))?.Contains("slide", StringComparison.OrdinalIgnoreCase) == true &&
                ((string?)element.Attribute("Target"))?.StartsWith("slides/", StringComparison.Ordinal) == true)
            .ToDictionary(
                element => (string?)element.Attribute("Id") ?? string.Empty,
                element => ((string?)element.Attribute("Target") ?? string.Empty).Replace("slides/", string.Empty, StringComparison.Ordinal),
                StringComparer.Ordinal);

        var presentation = XDocument.Load(archive.GetEntry("ppt/presentation.xml")!.Open());
        return presentation.Descendants(P + "sldId")
            .Select(element =>
            {
                var rid = (string?)element.Attribute(R + "id") ?? string.Empty;
                return ridToSlide.TryGetValue(rid, out var name)
                    ? (Name: name, Hidden: (string?)element.Attribute("show") == "0")
                    : default;
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToList();
    }

    private static HashSet<string> GetSlidesInPresentation(string unpackedDir)
    {
        var presentationPath = Path.Combine(unpackedDir, "ppt", "presentation.xml");
        var relsPath = Path.Combine(unpackedDir, "ppt", "_rels", "presentation.xml.rels");
        if (!File.Exists(presentationPath) || !File.Exists(relsPath))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var rels = XDocument.Load(relsPath);
        var ridToSlide = rels.Descendants(PkgRelationships + "Relationship")
            .Where(element =>
                ((string?)element.Attribute("Type"))?.Contains("slide", StringComparison.OrdinalIgnoreCase) == true &&
                ((string?)element.Attribute("Target"))?.StartsWith("slides/", StringComparison.Ordinal) == true)
            .ToDictionary(
                element => (string?)element.Attribute("Id") ?? string.Empty,
                element => ((string?)element.Attribute("Target") ?? string.Empty).Replace("slides/", string.Empty, StringComparison.Ordinal),
                StringComparer.Ordinal);

        var presentation = XDocument.Load(presentationPath);
        return presentation.Descendants(P + "sldId")
            .Select(element => (string?)element.Attribute(R + "id"))
            .Where(value => value is not null && ridToSlide.ContainsKey(value))
            .Select(value => ridToSlide[value!])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> RemoveOrphanedSlides(string unpackedDir)
    {
        var removed = new List<string>();
        var referencedSlides = GetSlidesInPresentation(unpackedDir);
        var slidesDirectory = Path.Combine(unpackedDir, "ppt", "slides");
        var relsDirectory = Path.Combine(slidesDirectory, "_rels");
        if (!Directory.Exists(slidesDirectory))
        {
            return removed;
        }

        foreach (var slide in Directory.GetFiles(slidesDirectory, "slide*.xml"))
        {
            var name = Path.GetFileName(slide);
            if (referencedSlides.Contains(name))
            {
                continue;
            }

            File.Delete(slide);
            removed.Add(Path.GetRelativePath(unpackedDir, slide));
            var rels = Path.Combine(relsDirectory, $"{name}.rels");
            if (File.Exists(rels))
            {
                File.Delete(rels);
                removed.Add(Path.GetRelativePath(unpackedDir, rels));
            }
        }

        var presRelsPath = Path.Combine(unpackedDir, "ppt", "_rels", "presentation.xml.rels");
        if (File.Exists(presRelsPath))
        {
            var rels = XDocument.Load(presRelsPath, LoadOptions.PreserveWhitespace);
            var changed = false;
            foreach (var relationship in rels.Root!.Elements(PkgRelationships + "Relationship")
                         .Where(element =>
                             ((string?)element.Attribute("Target"))?.StartsWith("slides/", StringComparison.Ordinal) == true &&
                             !referencedSlides.Contains(((string?)element.Attribute("Target"))!.Replace("slides/", string.Empty, StringComparison.Ordinal)))
                         .ToList())
            {
                relationship.Remove();
                changed = true;
            }

            if (changed)
            {
                rels.Save(presRelsPath, SaveOptions.DisableFormatting);
            }
        }

        return removed;
    }

    private static List<string> RemoveTrash(string unpackedDir)
    {
        var removed = new List<string>();
        var trashDirectory = Path.Combine(unpackedDir, "[trash]");
        if (!Directory.Exists(trashDirectory))
        {
            return removed;
        }

        foreach (var file in Directory.GetFiles(trashDirectory))
        {
            removed.Add(Path.GetRelativePath(unpackedDir, file));
            File.Delete(file);
        }

        Directory.Delete(trashDirectory);
        return removed;
    }

    private static HashSet<string> GetReferencedFiles(string unpackedDir)
    {
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relsFile in Directory.GetFiles(unpackedDir, "*.rels", SearchOption.AllDirectories))
        {
            var directory = Directory.GetParent(Path.GetDirectoryName(relsFile)!)?.FullName ?? unpackedDir;
            var rels = XDocument.Load(relsFile);
            foreach (var relationship in rels.Descendants(PkgRelationships + "Relationship"))
            {
                var target = (string?)relationship.Attribute("Target");
                if (string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                var targetPath = Path.GetFullPath(Path.Combine(directory, target.Replace('/', Path.DirectorySeparatorChar)));
                if (targetPath.StartsWith(Path.GetFullPath(unpackedDir), StringComparison.OrdinalIgnoreCase) && File.Exists(targetPath))
                {
                    referenced.Add(Path.GetRelativePath(unpackedDir, targetPath));
                }
            }
        }

        return referenced;
    }

    private static List<string> RemoveOrphanedRelationshipFiles(string unpackedDir)
    {
        var removed = new List<string>();
        var referenced = GetReferencedFiles(unpackedDir);
        foreach (var directoryName in new[] { "charts", "diagrams", "drawings" })
        {
            var relsDirectory = Path.Combine(unpackedDir, "ppt", directoryName, "_rels");
            if (!Directory.Exists(relsDirectory))
            {
                continue;
            }

            foreach (var relsFile in Directory.GetFiles(relsDirectory, "*.rels"))
            {
                var resourceFile = Path.Combine(Directory.GetParent(relsDirectory)!.FullName, Path.GetFileNameWithoutExtension(relsFile));
                var resourceRelative = Path.GetRelativePath(unpackedDir, resourceFile);
                if (!File.Exists(resourceFile) || !referenced.Contains(resourceRelative))
                {
                    File.Delete(relsFile);
                    removed.Add(Path.GetRelativePath(unpackedDir, relsFile));
                }
            }
        }

        return removed;
    }

    private static List<string> RemoveOrphanedFiles(string unpackedDir, HashSet<string> referenced)
    {
        var removed = new List<string>();
        foreach (var directoryName in new[] { "media", "embeddings", "charts", "diagrams", "tags", "drawings", "ink" })
        {
            var directory = Path.Combine(unpackedDir, "ppt", directoryName);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(directory))
            {
                var relative = Path.GetRelativePath(unpackedDir, file);
                if (!referenced.Contains(relative))
                {
                    File.Delete(file);
                    removed.Add(relative);
                }
            }
        }

        var themeDirectory = Path.Combine(unpackedDir, "ppt", "theme");
        if (Directory.Exists(themeDirectory))
        {
            foreach (var file in Directory.GetFiles(themeDirectory, "theme*.xml"))
            {
                var relative = Path.GetRelativePath(unpackedDir, file);
                if (!referenced.Contains(relative))
                {
                    File.Delete(file);
                    removed.Add(relative);
                    var rels = Path.Combine(themeDirectory, "_rels", $"{Path.GetFileName(file)}.rels");
                    if (File.Exists(rels))
                    {
                        File.Delete(rels);
                        removed.Add(Path.GetRelativePath(unpackedDir, rels));
                    }
                }
            }
        }

        var notesDirectory = Path.Combine(unpackedDir, "ppt", "notesSlides");
        if (Directory.Exists(notesDirectory))
        {
            foreach (var file in Directory.GetFiles(notesDirectory, "*.xml"))
            {
                var relative = Path.GetRelativePath(unpackedDir, file);
                if (!referenced.Contains(relative))
                {
                    File.Delete(file);
                    removed.Add(relative);
                }
            }

            var relsDirectory = Path.Combine(notesDirectory, "_rels");
            if (Directory.Exists(relsDirectory))
            {
                foreach (var file in Directory.GetFiles(relsDirectory, "*.rels"))
                {
                    var noteFile = Path.Combine(notesDirectory, Path.GetFileNameWithoutExtension(file));
                    if (!File.Exists(noteFile))
                    {
                        File.Delete(file);
                        removed.Add(Path.GetRelativePath(unpackedDir, file));
                    }
                }
            }
        }

        return removed;
    }

    private static void UpdateContentTypes(string unpackedDir, IReadOnlyCollection<string> removedFiles)
    {
        var path = Path.Combine(unpackedDir, "[Content_Types].xml");
        if (!File.Exists(path))
        {
            return;
        }

        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("[Content_Types].xml is empty.");
        var removedSet = removedFiles.Select(file => file.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var overrideElement in root.Elements(ContentTypes + "Override")
                     .Where(element => removedSet.Contains(((string?)element.Attribute("PartName") ?? string.Empty).TrimStart('/')))
                     .ToList())
        {
            overrideElement.Remove();
        }

        document.Save(path, SaveOptions.DisableFormatting);
    }
}
