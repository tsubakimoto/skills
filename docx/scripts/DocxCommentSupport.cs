using System.Globalization;
using System.Security;
using System.Xml.Linq;

internal static class DocxCommentSupport
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace W14 = "http://schemas.microsoft.com/office/word/2010/wordml";
    private static readonly XNamespace W15 = "http://schemas.microsoft.com/office/word/2012/wordml";
    private static readonly XNamespace W16Cid = "http://schemas.microsoft.com/office/word/2016/wordml/cid";
    private static readonly XNamespace W16Cex = "http://schemas.microsoft.com/office/word/2018/wordml/cex";
    private static readonly Random Random = new();

    public static (string ParaId, string Message) AddComment(
        string unpackedDir,
        int commentId,
        string text,
        string author,
        string initials,
        int? parentId)
    {
        var wordDirectory = Path.Combine(Path.GetFullPath(unpackedDir), "word");
        if (!Directory.Exists(wordDirectory))
        {
            return (string.Empty, $"Error: {wordDirectory} not found");
        }

        var paraId = GenerateHexId();
        var durableId = GenerateHexId();
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        var commentsPath = Path.Combine(wordDirectory, "comments.xml");
        if (!File.Exists(commentsPath))
        {
            CopyTemplate("comments.xml", commentsPath);
            EnsureCommentRelationships(Path.GetFullPath(unpackedDir));
            EnsureCommentContentTypes(Path.GetFullPath(unpackedDir));
        }

        AppendComment(commentsPath, commentId, text, author, initials, timestamp, paraId);

        var commentsExtendedPath = Path.Combine(wordDirectory, "commentsExtended.xml");
        if (!File.Exists(commentsExtendedPath))
        {
            CopyTemplate("commentsExtended.xml", commentsExtendedPath);
        }

        string? parentParaId = null;
        if (parentId is not null)
        {
            parentParaId = FindCommentParaId(commentsPath, parentId.Value);
            if (parentParaId is null)
            {
                return (string.Empty, $"Error: Parent comment {parentId.Value} not found");
            }
        }

        AppendCommentExtended(commentsExtendedPath, paraId, parentParaId);

        var commentsIdsPath = Path.Combine(wordDirectory, "commentsIds.xml");
        if (!File.Exists(commentsIdsPath))
        {
            CopyTemplate("commentsIds.xml", commentsIdsPath);
        }

        AppendCommentId(commentsIdsPath, paraId, durableId);

        var commentsExtensiblePath = Path.Combine(wordDirectory, "commentsExtensible.xml");
        if (!File.Exists(commentsExtensiblePath))
        {
            CopyTemplate("commentsExtensible.xml", commentsExtensiblePath);
        }

        AppendCommentExtensible(commentsExtensiblePath, durableId, timestamp);
        return (paraId, $"Added {(parentId is null ? "comment" : "reply")} {commentId} (para_id={paraId})");
    }

    public static string GetCommentMarkerTemplate(int commentId) => $"""
Add to document.xml (markers must be direct children of w:p, never inside w:r):
  <w:commentRangeStart w:id="{commentId}"/>
  <w:r>...</w:r>
  <w:commentRangeEnd w:id="{commentId}"/>
  <w:r><w:rPr><w:rStyle w:val="CommentReference"/></w:rPr><w:commentReference w:id="{commentId}"/></w:r>
""";

    public static string GetReplyMarkerTemplate(int parentId, int commentId) => $"""
Nest markers inside parent {parentId}'s markers (markers must be direct children of w:p, never inside w:r):
  <w:commentRangeStart w:id="{parentId}"/><w:commentRangeStart w:id="{commentId}"/>
  <w:r>...</w:r>
  <w:commentRangeEnd w:id="{commentId}"/><w:commentRangeEnd w:id="{parentId}"/>
  <w:r><w:rPr><w:rStyle w:val="CommentReference"/></w:rPr><w:commentReference w:id="{parentId}"/></w:r>
  <w:r><w:rPr><w:rStyle w:val="CommentReference"/></w:rPr><w:commentReference w:id="{commentId}"/></w:r>
""";

    private static string GenerateHexId() => Random.NextInt64(0, 0x7FFFFFFF).ToString("X8", CultureInfo.InvariantCulture);

    private static void AppendComment(string commentsPath, int commentId, string text, string author, string initials, string timestamp, string paraId)
    {
        var document = XDocument.Load(commentsPath, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("comments.xml is empty.");
        root.Add(
            new XElement(W + "comment",
                new XAttribute(W + "id", commentId),
                new XAttribute(W + "author", author),
                new XAttribute(W + "date", timestamp),
                new XAttribute(W + "initials", initials),
                new XElement(W + "p",
                    new XAttribute(W14 + "paraId", paraId),
                    new XAttribute(W14 + "textId", "77777777"),
                    new XElement(W + "r",
                        new XElement(W + "rPr", new XElement(W + "rStyle", new XAttribute(W + "val", "CommentReference"))),
                        new XElement(W + "annotationRef")),
                    new XElement(W + "r",
                        new XElement(W + "rPr",
                            new XElement(W + "color", new XAttribute(W + "val", "000000")),
                            new XElement(W + "sz", new XAttribute(W + "val", "20")),
                            new XElement(W + "szCs", new XAttribute(W + "val", "20"))),
                        new XElement(W + "t", SecurityElement.Escape(text) ?? string.Empty)))));
        document.Save(commentsPath, SaveOptions.DisableFormatting);
        EscapeSmartQuotes(commentsPath);
    }

    private static void AppendCommentExtended(string path, string paraId, string? parentParaId)
    {
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("commentsExtended.xml is empty.");
        var element = new XElement(W15 + "commentEx",
            new XAttribute(W15 + "paraId", paraId),
            new XAttribute(W15 + "done", "0"));
        if (!string.IsNullOrWhiteSpace(parentParaId))
        {
            element.Add(new XAttribute(W15 + "paraIdParent", parentParaId));
        }

        root.Add(element);
        document.Save(path, SaveOptions.DisableFormatting);
    }

    private static void AppendCommentId(string path, string paraId, string durableId)
    {
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        document.Root!.Add(new XElement(W16Cid + "commentId",
            new XAttribute(W16Cid + "paraId", paraId),
            new XAttribute(W16Cid + "durableId", durableId)));
        document.Save(path, SaveOptions.DisableFormatting);
    }

    private static void AppendCommentExtensible(string path, string durableId, string timestamp)
    {
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        document.Root!.Add(new XElement(W16Cex + "commentExtensible",
            new XAttribute(W16Cex + "durableId", durableId),
            new XAttribute(W16Cex + "dateUtc", timestamp)));
        document.Save(path, SaveOptions.DisableFormatting);
    }

    private static string? FindCommentParaId(string commentsPath, int commentId)
    {
        var document = XDocument.Load(commentsPath);
        return document.Descendants(W + "comment")
            .FirstOrDefault(element => (string?)element.Attribute(W + "id") == commentId.ToString(CultureInfo.InvariantCulture))
            ?.Descendants(W + "p")
            .Select(element => (string?)element.Attribute(W14 + "paraId"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static void EnsureCommentRelationships(string unpackedDir)
    {
        var relsPath = Path.Combine(unpackedDir, "word", "_rels", "document.xml.rels");
        if (!File.Exists(relsPath))
        {
            return;
        }

        XNamespace pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
        var document = XDocument.Load(relsPath, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("document.xml.rels is empty.");
        if (root.Elements(pkg + "Relationship").Any(element => (string?)element.Attribute("Target") == "comments.xml"))
        {
            return;
        }

        var nextId = root.Elements(pkg + "Relationship")
            .Select(element => (string?)element.Attribute("Id"))
            .Where(value => value?.StartsWith("rId", StringComparison.Ordinal) == true)
            .Select(value => int.TryParse(value![3..], out var parsed) ? parsed : 0)
            .DefaultIfEmpty()
            .Max() + 1;

        foreach (var (type, target) in new[]
        {
            ("http://schemas.openxmlformats.org/officeDocument/2006/relationships/comments", "comments.xml"),
            ("http://schemas.microsoft.com/office/2011/relationships/commentsExtended", "commentsExtended.xml"),
            ("http://schemas.microsoft.com/office/2016/09/relationships/commentsIds", "commentsIds.xml"),
            ("http://schemas.microsoft.com/office/2018/08/relationships/commentsExtensible", "commentsExtensible.xml")
        })
        {
            root.Add(new XElement(pkg + "Relationship",
                new XAttribute("Id", $"rId{nextId++}"),
                new XAttribute("Type", type),
                new XAttribute("Target", target)));
        }

        document.Save(relsPath, SaveOptions.DisableFormatting);
    }

    private static void EnsureCommentContentTypes(string unpackedDir)
    {
        var path = Path.Combine(unpackedDir, "[Content_Types].xml");
        if (!File.Exists(path))
        {
            return;
        }

        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("[Content_Types].xml is empty.");
        if (root.Elements(ns + "Override").Any(element => (string?)element.Attribute("PartName") == "/word/comments.xml"))
        {
            return;
        }

        foreach (var (partName, contentType) in new[]
        {
            ("/word/comments.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.comments+xml"),
            ("/word/commentsExtended.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.commentsExtended+xml"),
            ("/word/commentsIds.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.commentsIds+xml"),
            ("/word/commentsExtensible.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.commentsExtensible+xml")
        })
        {
            root.Add(new XElement(ns + "Override",
                new XAttribute("PartName", partName),
                new XAttribute("ContentType", contentType)));
        }

        document.Save(path, SaveOptions.DisableFormatting);
    }

    private static void CopyTemplate(string templateName, string destinationPath)
    {
        var source = Path.Combine(OfficeSupport.SourceDirectory(), "templates", templateName);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(source, destinationPath, overwrite: true);
    }

    private static void EscapeSmartQuotes(string path)
    {
        var content = File.ReadAllText(path);
        content = content
            .Replace("\u201c", "&#x201C;", StringComparison.Ordinal)
            .Replace("\u201d", "&#x201D;", StringComparison.Ordinal)
            .Replace("\u2018", "&#x2018;", StringComparison.Ordinal)
            .Replace("\u2019", "&#x2019;", StringComparison.Ordinal);
        File.WriteAllText(path, content);
    }
}
