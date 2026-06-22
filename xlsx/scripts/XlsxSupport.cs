using System.IO.Compression;
using System.Xml.Linq;

internal static class XlsxSupport
{
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly Dictionary<string, string> ErrorMap = new(StringComparer.Ordinal)
    {
        ["#VALUE!"] = "#VALUE!",
        ["#DIV/0!"] = "#DIV/0!",
        ["#REF!"] = "#REF!",
        ["#NAME?"] = "#NAME?",
        ["#NULL!"] = "#NULL!",
        ["#NUM!"] = "#NUM!",
        ["#N/A"] = "#N/A"
    };

    public static Dictionary<string, object> InspectWorkbook(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var workbook = XDocument.Load(archive.GetEntry("xl/workbook.xml")!.Open());
        var workbookRels = XDocument.Load(archive.GetEntry("xl/_rels/workbook.xml.rels")!.Open());
        var sharedStrings = LoadSharedStrings(archive);
        var relMap = workbookRels.Descendants().Where(element => element.Name.LocalName == "Relationship")
            .ToDictionary(
                element => (string?)element.Attribute("Id") ?? string.Empty,
                element => ((string?)element.Attribute("Target") ?? string.Empty).Replace("\\", "/", StringComparison.Ordinal),
                StringComparer.Ordinal);

        var sheetNames = workbook.Descendants(Spreadsheet + "sheet")
            .Select(element => new
            {
                Name = (string?)element.Attribute("name") ?? "Sheet",
                RelationshipId = (string?)element.Attribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"))
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.RelationshipId) && relMap.ContainsKey(item.RelationshipId!))
            .ToList();

        var errorSummary = ErrorMap.Keys.ToDictionary(key => key, _ => new List<string>(), StringComparer.Ordinal);
        var totalFormulas = 0;
        foreach (var sheet in sheetNames)
        {
            var target = relMap[sheet.RelationshipId!];
            var entryPath = target.StartsWith("xl/", StringComparison.Ordinal) ? target : $"xl/{target.TrimStart('/')}";
            var sheetDocument = XDocument.Load(archive.GetEntry(entryPath)! .Open());
            foreach (var cell in sheetDocument.Descendants(Spreadsheet + "c"))
            {
                var coordinate = (string?)cell.Attribute("r") ?? string.Empty;
                if (cell.Element(Spreadsheet + "f") is not null)
                {
                    totalFormulas++;
                }

                var rawValue = GetCellValue(cell, sharedStrings);
                if (rawValue is null)
                {
                    continue;
                }

                foreach (var error in ErrorMap.Keys)
                {
                    if (rawValue.Contains(error, StringComparison.Ordinal))
                    {
                        errorSummary[error].Add($"{sheet.Name}!{coordinate}");
                        break;
                    }
                }
            }
        }

        var totalErrors = errorSummary.Sum(pair => pair.Value.Count);
        var compactSummary = errorSummary
            .Where(pair => pair.Value.Count > 0)
            .ToDictionary(
                pair => pair.Key,
                pair => (object)new Dictionary<string, object>
                {
                    ["count"] = pair.Value.Count,
                    ["locations"] = pair.Value.Take(20).ToArray()
                },
                StringComparer.Ordinal);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["status"] = totalErrors == 0 ? "success" : "errors_found",
            ["total_errors"] = totalErrors,
            ["error_summary"] = compactSummary,
            ["total_formulas"] = totalFormulas
        };
    }

    private static List<string> LoadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        var document = XDocument.Load(entry.Open());
        return document.Descendants(Spreadsheet + "si")
            .Select(item => string.Concat(item.Descendants(Spreadsheet + "t").Select(text => text.Value)))
            .ToList();
    }

    private static string? GetCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr")
        {
            return string.Concat(cell.Descendants(Spreadsheet + "t").Select(text => text.Value));
        }

        var value = cell.Element(Spreadsheet + "v")?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (type == "s" && int.TryParse(value, out var index) && index >= 0 && index < sharedStrings.Count)
        {
            return sharedStrings[index];
        }

        return value;
    }
}
