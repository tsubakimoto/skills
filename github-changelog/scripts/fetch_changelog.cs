#:property PublishAot=false

using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

Console.OutputEncoding = System.Text.Encoding.UTF8;

return await ChangelogFeedCli.RunAsync(args);

static class ChangelogFeedCli
{
    private const string FeedUrl = "https://github.blog/changelog/feed/";
    private static readonly XNamespace ContentNamespace = "http://purl.org/rss/1.0/modules/content/";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args.Any(IsHelpToken))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var targetDate = args[0];
        if (!DateOnly.TryParseExact(targetDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            Console.Error.WriteLine($"Error: Invalid date '{targetDate}'. Use YYYY-MM-DD format.");
            return 1;
        }

        Console.Error.WriteLine($"Fetching {FeedUrl} ...");

        string xmlText;
        try
        {
            xmlText = await FetchFeedAsync(FeedUrl);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching feed: {ex.Message}");
            return 1;
        }

        List<ChangelogEntry> entries;
        try
        {
            entries = ExtractEntries(xmlText, targetDate);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error parsing feed: {ex.Message}");
            return 1;
        }

        Console.Error.WriteLine($"Found {entries.Count} entries for {targetDate}.");
        Console.WriteLine(JsonSerializer.Serialize(entries, JsonOptions));
        return 0;
    }

    private static async Task<string> FetchFeedAsync(string url)
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("github-changelog-skill/1.0");

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static List<ChangelogEntry> ExtractEntries(string xmlText, string targetDate)
    {
        var document = XDocument.Parse(SanitizeXmlEntities(xmlText), LoadOptions.PreserveWhitespace);
        var root = document.Root;
        if (root is null)
        {
            return [];
        }

        return IsRss(root)
            ? ExtractRssEntries(root, targetDate)
            : ExtractAtomEntries(root, targetDate);
    }

    private static List<ChangelogEntry> ExtractRssEntries(XElement root, string targetDate)
    {
        var entries = new List<ChangelogEntry>();
        var channel = root.Element("channel");
        if (channel is null)
        {
            return entries;
        }

        foreach (var item in channel.Elements("item"))
        {
            var published = ParseDate(GetElementValue(item, "pubDate"));
            if (published is null || published.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) != targetDate)
            {
                continue;
            }

            var description = GetElementValue(item, ContentNamespace + "encoded");
            if (string.IsNullOrWhiteSpace(description))
            {
                description = GetElementValue(item, "description");
            }

            entries.Add(new ChangelogEntry(
                GetElementValue(item, "title"),
                GetElementValue(item, "link"),
                targetDate,
                StripHtml(description)));
        }

        return entries;
    }

    private static List<ChangelogEntry> ExtractAtomEntries(XElement root, string targetDate)
    {
        var entries = new List<ChangelogEntry>();
        XNamespace atomNs = root.Name.Namespace;

        foreach (var entry in root.Elements(atomNs + "entry"))
        {
            var dateText = GetElementValue(entry, atomNs + "published");
            if (string.IsNullOrWhiteSpace(dateText))
            {
                dateText = GetElementValue(entry, atomNs + "updated");
            }

            var published = ParseDate(dateText);
            if (published is null || published.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) != targetDate)
            {
                continue;
            }

            var linkElement = entry.Elements(atomNs + "link")
                .FirstOrDefault(element => string.Equals((string?)element.Attribute("rel"), "alternate", StringComparison.OrdinalIgnoreCase))
                ?? entry.Element(atomNs + "link");
            var link = (string?)linkElement?.Attribute("href") ?? string.Empty;

            var content = GetElementValue(entry, atomNs + "content");
            if (string.IsNullOrWhiteSpace(content))
            {
                content = GetElementValue(entry, atomNs + "summary");
            }

            entries.Add(new ChangelogEntry(
                GetElementValue(entry, atomNs + "title"),
                link,
                targetDate,
                StripHtml(content)));
        }

        return entries;
    }

    private static bool IsRss(XElement root) =>
        string.Equals(root.Name.LocalName, "rss", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        string[] formats =
        [
            "yyyy-MM-dd'T'HH:mm:ssK",
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            "yyyy-MM-dd"
        ];

        foreach (var format in formats)
        {
            if (DateTimeOffset.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string StripHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var stripped = Regex.Replace(text, "<[^>]+>", " ");
        stripped = WebUtility.HtmlDecode(stripped);
        return Regex.Replace(stripped, @"\s+", " ").Trim();
    }

    private static string SanitizeXmlEntities(string xmlText) =>
        Regex.Replace(
            xmlText,
            @"&(?<name>[A-Za-z][A-Za-z0-9]+);",
            match =>
            {
                var name = match.Groups["name"].Value;
                if (name is "amp" or "lt" or "gt" or "quot" or "apos")
                {
                    return match.Value;
                }

                var decoded = WebUtility.HtmlDecode(match.Value);
                return decoded == match.Value
                    ? match.Value
                    : System.Security.SecurityElement.Escape(decoded) ?? decoded;
            });

    private static string GetElementValue(XElement element, XName name) =>
        element.Element(name)?.Value?.Trim() ?? string.Empty;

    private static bool IsHelpToken(string token) =>
        string.Equals(token, "--help", StringComparison.Ordinal) ||
        string.Equals(token, "-h", StringComparison.Ordinal);

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Fetch GitHub Changelog RSS entries for a specified date.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  dotnet run --file scripts\\fetch_changelog.cs -- <YYYY-MM-DD>");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Outputs a JSON array of matching entries to stdout.");
        Console.Error.WriteLine("Each entry has: title, link, date, description.");
    }

    private sealed record ChangelogEntry(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("link")] string Link,
        [property: JsonPropertyName("date")] string Date,
        [property: JsonPropertyName("description")] string Description);
}
