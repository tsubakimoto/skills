#:property PublishAot=false

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

Console.OutputEncoding = System.Text.Encoding.UTF8;

return await DevBlogFeedCli.RunAsync(args);

static class DevBlogFeedCli
{
    private const string SiteRoot = "https://devblogs.microsoft.com";
    private const string LandingFeedUrl = $"{SiteRoot}/landing";
    private const string RobotsUrl = $"{SiteRoot}/robots.txt";
    private const string AllBlogsUrl = $"{SiteRoot}/wp-json/custom/v1/all-blogs";

    private static readonly XNamespace ContentNamespace = "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace DcNamespace = "http://purl.org/dc/elements/1.1/";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Path segments that are never blog slugs on devblogs.microsoft.com.
    private static readonly HashSet<string> ReservedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "wp-json", "wp-content", "wp-includes", "wp-admin", "landing", "feed",
        "author", "tag", "category", "page", "sitemap.xml", "sitemap_index.xml", "robots.txt"
    };

    // Blogs that are not always advertised by robots.txt or all-blogs, including
    // aggregate-only blogs such as vscode-blog and external-blogs.
    private static readonly string[] SeedSlugs =
    [
        "dotnet", "visualstudio", "vscode-blog", "external-blogs", "blog", "oldnewthing",
        "cppblog", "commandline", "devops", "typescript", "powershell", "python", "java",
        "go", "identity", "azure-sql", "cosmosdb", "foundry", "aspire", "agent-framework",
        "microsoft365dev", "directx", "pix", "ise", "performance-diagnostics", "ifdef-windows"
    ];

    private static readonly Dictionary<string, string> TokenRewrites = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ai"] = "AI",
        ["api"] = "API",
        ["aspnet"] = "ASP.NET",
        ["azure"] = "Azure",
        ["blog"] = "Blog",
        ["copilot"] = "Copilot",
        ["cpp"] = "C++",
        ["csharp"] = "C#",
        ["css"] = "CSS",
        ["devops"] = "DevOps",
        ["dotnet"] = ".NET",
        ["github"] = "GitHub",
        ["go"] = "Go",
        ["html"] = "HTML",
        ["ios"] = "iOS",
        ["javascript"] = "JavaScript",
        ["mcp"] = "MCP",
        ["microsoft"] = "Microsoft",
        ["sql"] = "SQL",
        ["vs"] = "VS",
        ["visualstudio"] = "Visual Studio",
        ["vscode"] = "VS Code",
        ["windows"] = "Windows",
        ["xml"] = "XML"
    };

    private static readonly HttpClient Http = CreateHttpClient();

    private static readonly ConcurrentDictionary<string, string> AuthorCache = new(StringComparer.Ordinal);

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args.Any(IsHelpToken))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine();
            PrintUsage();
            return 1;
        }

        Console.Error.WriteLine(
            $"Target: {options.From:yyyy-MM-dd} .. {options.To:yyyy-MM-dd} (offset {FormatOffset(options.Offset)})");

        var blogs = await DiscoverBlogsAsync(options);
        Console.Error.WriteLine($"Discovered {blogs.Count} blog slugs.");

        var collected = new ConcurrentDictionary<string, DevBlogEntry>(StringComparer.Ordinal);
        var diagnostics = new ConcurrentBag<string>();

        var feedCount = await CollectFromLandingFeedAsync(options, collected, diagnostics);
        Console.Error.WriteLine($"Landing feed: {feedCount} matching entries.");

        var restCount = await CollectFromRestAsync(options, blogs, collected, diagnostics);
        Console.Error.WriteLine($"Blog REST APIs: {restCount} matching entries.");

        var entries = CollapseCrossPosts(collected.Values)
            .OrderByDescending(entry => entry.PublishedAt, StringComparer.Ordinal)
            .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.Error.WriteLine($"Found {entries.Count} unique entries after deduplication.");

        if (options.Diagnostics)
        {
            foreach (var line in diagnostics.OrderBy(text => text, StringComparer.Ordinal))
            {
                Console.Error.WriteLine($"  {line}");
            }
        }
        else
        {
            var failures = diagnostics.Count(line => line.StartsWith("FAIL", StringComparison.Ordinal));
            if (failures > 0)
            {
                Console.Error.WriteLine($"{failures} source(s) failed. Re-run with --diagnostics for details.");
            }
        }

        Console.WriteLine(JsonSerializer.Serialize(entries, JsonOptions));
        return 0;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = DecompressionMethods.All
        };

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("devblog-updates-skill/2.0");
        return client;
    }

    // ---------------------------------------------------------------- discovery

    private static async Task<Dictionary<string, string>> DiscoverBlogsAsync(Options options)
    {
        // slug -> display name (empty when unknown)
        var blogs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var slug in SeedSlugs)
        {
            AddSlug(blogs, slug, string.Empty);
        }

        // robots.txt lists one Sitemap entry per multisite blog: the most complete inventory available.
        try
        {
            var robots = await Http.GetStringAsync(RobotsUrl);
            foreach (Match match in Regex.Matches(robots, @"Sitemap:\s*https://devblogs\.microsoft\.com/(?<slug>[^/\s]+)/sitemap", RegexOptions.IgnoreCase))
            {
                AddSlug(blogs, match.Groups["slug"].Value, string.Empty);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: robots.txt discovery failed ({ex.Message}).");
        }

        // custom/v1/all-blogs adds human readable blog names.
        try
        {
            using var document = JsonDocument.Parse(await Http.GetStringAsync(AllBlogsUrl));
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    var url = GetString(item, "url");
                    var name = GetString(item, "name");
                    AddSlug(blogs, InferBlogSlug(url), name);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: all-blogs discovery failed ({ex.Message}).");
        }

        if (options.OnlySlugs.Count > 0)
        {
            return options.OnlySlugs.ToDictionary(
                slug => slug,
                slug => blogs.TryGetValue(slug, out var name) ? name : string.Empty,
                StringComparer.OrdinalIgnoreCase);
        }

        return blogs;
    }

    private static void AddSlug(Dictionary<string, string> blogs, string slug, string name)
    {
        if (string.IsNullOrWhiteSpace(slug) || ReservedSegments.Contains(slug))
        {
            return;
        }

        if (!blogs.TryGetValue(slug, out var existing) || (string.IsNullOrWhiteSpace(existing) && !string.IsNullOrWhiteSpace(name)))
        {
            blogs[slug] = name;
        }
    }

    // ------------------------------------------------------------- landing feed

    private static async Task<int> CollectFromLandingFeedAsync(
        Options options,
        ConcurrentDictionary<string, DevBlogEntry> collected,
        ConcurrentBag<string> diagnostics)
    {
        string xmlText;
        try
        {
            // The landing feed sits behind a CDN cache that can serve a stale variant,
            // so a unique query string is used to force a fresh copy.
            xmlText = await Http.GetStringAsync($"{LandingFeedUrl}/?cb={Guid.NewGuid():N}");
        }
        catch (Exception ex)
        {
            diagnostics.Add($"FAIL landing-feed: {ex.Message}");
            Console.Error.WriteLine($"Warning: landing feed fetch failed ({ex.Message}).");
            return 0;
        }

        List<DevBlogEntry> entries;
        try
        {
            entries = ParseFeed(xmlText, options);
        }
        catch (Exception ex)
        {
            diagnostics.Add($"FAIL landing-feed-parse: {ex.Message}");
            Console.Error.WriteLine($"Warning: landing feed parse failed ({ex.Message}).");
            return 0;
        }

        foreach (var entry in entries)
        {
            Merge(collected, entry);
        }

        diagnostics.Add($"OK   landing-feed: {entries.Count} matched");
        return entries.Count;
    }

    private static List<DevBlogEntry> ParseFeed(string xmlText, Options options)
    {
        var document = XDocument.Parse(SanitizeXmlEntities(xmlText), LoadOptions.PreserveWhitespace);
        var root = document.Root;
        if (root is null)
        {
            return [];
        }

        return IsRss(root)
            ? ParseRssItems(root, options)
            : ParseAtomEntries(root, options);
    }

    private static List<DevBlogEntry> ParseRssItems(XElement root, Options options)
    {
        var entries = new List<DevBlogEntry>();
        var channel = root.Element("channel");
        if (channel is null)
        {
            return entries;
        }

        foreach (var item in channel.Elements("item"))
        {
            var published = ParseDate(GetElementValue(item, "pubDate"));
            var link = GetElementValue(item, "link");
            if (published is null || string.IsNullOrWhiteSpace(link) || !options.Contains(published.Value))
            {
                continue;
            }

            var description = GetElementValue(item, "description")
                .IfEmpty(GetElementValue(item, ContentNamespace + "encoded"));

            var slug = InferBlogSlug(link);
            entries.Add(new DevBlogEntry(
                StripHtml(GetElementValue(item, "title")),
                link,
                options.LocalDate(published.Value),
                ToUtcIsoFormat(published.Value),
                FormatBlogName(slug),
                slug,
                GetElementValue(item, DcNamespace + "creator").IfEmpty(GetElementValue(item, "author")),
                Summarize(description, options),
                "landing-feed"));
        }

        return entries;
    }

    private static List<DevBlogEntry> ParseAtomEntries(XElement root, Options options)
    {
        var entries = new List<DevBlogEntry>();
        XNamespace atomNs = root.Name.Namespace;

        foreach (var entry in root.Elements(atomNs + "entry"))
        {
            var dateText = GetElementValue(entry, atomNs + "published")
                .IfEmpty(GetElementValue(entry, atomNs + "updated"));
            var published = ParseDate(dateText);
            if (published is null || !options.Contains(published.Value))
            {
                continue;
            }

            var linkElement = entry.Elements(atomNs + "link")
                .FirstOrDefault(element => string.Equals((string?)element.Attribute("rel"), "alternate", StringComparison.OrdinalIgnoreCase))
                ?? entry.Element(atomNs + "link");
            var link = (string?)linkElement?.Attribute("href") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(link))
            {
                continue;
            }

            var content = GetElementValue(entry, atomNs + "summary")
                .IfEmpty(GetElementValue(entry, atomNs + "content"));
            var slug = InferBlogSlug(link);

            entries.Add(new DevBlogEntry(
                StripHtml(GetElementValue(entry, atomNs + "title")),
                link,
                options.LocalDate(published.Value),
                ToUtcIsoFormat(published.Value),
                FormatBlogName(slug),
                slug,
                entry.Element(atomNs + "author")?.Element(atomNs + "name")?.Value?.Trim() ?? string.Empty,
                Summarize(content, options),
                "landing-feed"));
        }

        return entries;
    }

    // ----------------------------------------------------------------- REST API

    private static async Task<int> CollectFromRestAsync(
        Options options,
        Dictionary<string, string> blogs,
        ConcurrentDictionary<string, DevBlogEntry> collected,
        ConcurrentBag<string> diagnostics)
    {
        var matched = 0;
        using var throttle = new SemaphoreSlim(options.Concurrency);

        var tasks = blogs.Select(async blog =>
        {
            await throttle.WaitAsync();
            try
            {
                var entries = await FetchBlogPostsAsync(blog.Key, blog.Value, options);
                foreach (var entry in entries)
                {
                    Merge(collected, entry);
                }

                Interlocked.Add(ref matched, entries.Count);
                diagnostics.Add($"OK   {blog.Key}: {entries.Count} matched");
            }
            catch (RestUnavailableException)
            {
                diagnostics.Add($"SKIP {blog.Key}: no REST API (landing feed only)");
            }
            catch (Exception ex)
            {
                diagnostics.Add($"FAIL {blog.Key}: {ex.Message}");
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
        return matched;
    }

    private static async Task<List<DevBlogEntry>> FetchBlogPostsAsync(string slug, string blogName, Options options)
    {
        var entries = new List<DevBlogEntry>();

        // WP's after/before filter uses site-local time and is silently dropped on some
        // redirected blogs, so query a padded window and always re-filter on the client.
        var after = options.From.AddDays(-2).ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        var before = options.To.AddDays(2).ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        var stopBefore = options.WindowStartUtc.AddDays(-2);

        // The multisite root hosts the "blog" site, which has no /blog/wp-json prefix.
        var restRoot = string.Equals(slug, "blog", StringComparison.OrdinalIgnoreCase)
            ? SiteRoot
            : $"{SiteRoot}/{slug}";

        for (var page = 1; page <= options.MaxPages; page++)
        {
            var url = $"{restRoot}/wp-json/wp/v2/posts" +
                      $"?per_page=100&page={page}&orderby=date&order=desc" +
                      $"&after={Uri.EscapeDataString(after)}&before={Uri.EscapeDataString(before)}" +
                      "&_fields=link,date_gmt,title,excerpt,author";

            using var response = await Http.GetAsync(url);

            // WP answers 400 (rest_post_invalid_page_number) once pagination is exhausted.
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                break;
            }

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone
                or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                throw new RestUnavailableException();
            }

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync();

            // Retired or aggregate-only blogs redirect the REST route to an HTML page.
            if (!payload.TrimStart().StartsWith('['))
            {
                throw new RestUnavailableException();
            }

            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new RestUnavailableException();
            }

            var count = 0;
            DateTimeOffset? oldestOnPage = null;

            foreach (var post in document.RootElement.EnumerateArray())
            {
                count++;

                var link = GetString(post, "link");
                var published = ParseDate(NormalizeGmt(GetString(post, "date_gmt")));
                if (string.IsNullOrWhiteSpace(link) || published is null)
                {
                    continue;
                }

                if (oldestOnPage is null || published < oldestOnPage)
                {
                    oldestOnPage = published;
                }

                if (!options.Contains(published.Value))
                {
                    continue;
                }

                var linkSlug = InferBlogSlug(link).IfEmpty(slug);
                var displayName = string.Equals(linkSlug, slug, StringComparison.OrdinalIgnoreCase)
                    ? blogName.IfEmpty(FormatBlogName(linkSlug))
                    : FormatBlogName(linkSlug);

                entries.Add(new DevBlogEntry(
                    StripHtml(GetRendered(post, "title")),
                    link,
                    options.LocalDate(published.Value),
                    ToUtcIsoFormat(published.Value),
                    displayName,
                    linkSlug,
                    await ResolveAuthorAsync(slug, post),
                    Summarize(GetRendered(post, "excerpt"), options),
                    $"rest:{slug}"));
            }

            if (count < 100 || (oldestOnPage is not null && oldestOnPage < stopBefore))
            {
                break;
            }
        }

        return entries;
    }

    private static async Task<string> ResolveAuthorAsync(string slug, JsonElement post)
    {
        if (!post.TryGetProperty("author", out var authorProperty) ||
            authorProperty.ValueKind != JsonValueKind.Number ||
            !authorProperty.TryGetInt64(out var authorId) ||
            authorId <= 0)
        {
            return string.Empty;
        }

        var key = $"{slug}/{authorId}";
        if (AuthorCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var name = string.Empty;
        try
        {
            using var response = await Http.GetAsync($"{SiteRoot}/{slug}/wp-json/wp/v2/users/{authorId}?_fields=name");
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                name = GetString(document.RootElement, "name");
            }
        }
        catch
        {
            // Author metadata is optional; leave it empty when the endpoint is unavailable.
        }

        AuthorCache[key] = name;
        return name;
    }

    private sealed class RestUnavailableException : Exception;

    // ------------------------------------------------------------------- merge

    private static void Merge(ConcurrentDictionary<string, DevBlogEntry> collected, DevBlogEntry entry) =>
        collected.AddOrUpdate(NormalizeLink(entry.Link), entry, (_, existing) => Richer(existing, entry));

    /// <summary>
    /// Some announcements are syndicated to several blogs (for example vscode-blog and
    /// external-blogs) under different permalinks. Collapse them by title and date.
    /// </summary>
    private static IEnumerable<DevBlogEntry> CollapseCrossPosts(IEnumerable<DevBlogEntry> entries)
    {
        var byTitle = new Dictionary<string, DevBlogEntry>(StringComparer.Ordinal);

        // "external-blogs" only re-publishes other blogs, so it never wins as the canonical entry.
        var ordered = entries
            .OrderBy(entry => string.Equals(entry.BlogSlug, "external-blogs", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(entry => entry.BlogSlug, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in ordered)
        {
            var key = $"{entry.Date}|{Regex.Replace(entry.Title, @"\s+", " ").Trim().ToLowerInvariant()}";

            if (string.IsNullOrWhiteSpace(entry.Title) || !byTitle.TryGetValue(key, out var existing))
            {
                byTitle[string.IsNullOrWhiteSpace(entry.Title) ? entry.Link : key] = entry;
                continue;
            }

            byTitle[key] = Richer(existing, entry);
        }

        return byTitle.Values;
    }

    private static DevBlogEntry Richer(DevBlogEntry existing, DevBlogEntry candidate) => existing with
    {
        Title = existing.Title.IfEmpty(candidate.Title),
        Author = existing.Author.IfEmpty(candidate.Author),
        Description = candidate.Description.Length > existing.Description.Length ? candidate.Description : existing.Description,
        Source = string.Equals(existing.Source, candidate.Source, StringComparison.Ordinal)
            ? existing.Source
            : $"{existing.Source}+{candidate.Source}"
    };

    private static string NormalizeLink(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return link.Trim().ToLowerInvariant();
        }

        var normalized = $"{uri.Host}{uri.AbsolutePath}".ToLowerInvariant().TrimEnd('/');
        var query = uri.Query.TrimStart('?');

        // Permalinks such as /oldnewthing/20260723-00/?p=112560 need the id to stay unique.
        if (query.StartsWith("p=", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"{normalized}?{query.ToLowerInvariant()}";
        }

        return normalized;
    }

    // ------------------------------------------------------------------ helpers

    private static bool IsRss(XElement root) =>
        string.Equals(root.Name.LocalName, "rss", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeGmt(string value) =>
        string.IsNullOrWhiteSpace(value) || value.EndsWith('Z') || Regex.IsMatch(value, @"[+-]\d{2}:?\d{2}$")
            ? value
            : value + "Z";

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
            "yyyy-MM-dd HH:mm:ss",
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

    private static string ToUtcIsoFormat(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Summarize(string? html, Options options)
    {
        var text = StripHtml(html);
        return options.MaxDescriptionLength > 0 && text.Length > options.MaxDescriptionLength
            ? text[..options.MaxDescriptionLength].TrimEnd() + "…"
            : text;
    }

    private static string StripHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var stripped = Regex.Replace(text, "<script[^>]*>.*?</script>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        stripped = Regex.Replace(stripped, "<style[^>]*>.*?</style>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        stripped = Regex.Replace(stripped, "<[^>]+>", " ");
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

    private static string FormatBlogName(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return "Microsoft Developer Blogs";
        }

        var parts = slug
            .Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => TokenRewrites.TryGetValue(token, out var mapped) ? mapped : Capitalize(token));

        return string.Join(' ', parts);
    }

    private static string Capitalize(string token) =>
        token.Length == 0
            ? token
            : char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant();

    private static string InferBlogSlug(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var path = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var slashIndex = path.IndexOf('/');
        return slashIndex >= 0 ? path[..slashIndex] : path;
    }

    private static string GetElementValue(XElement element, XName name) =>
        element.Element(name)?.Value?.Trim() ?? string.Empty;

    private static string GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;

    private static string GetRendered(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString()?.Trim() ?? string.Empty,
                JsonValueKind.Object => GetString(value, "rendered"),
                _ => string.Empty
            }
            : string.Empty;

    private static string FormatOffset(TimeSpan offset) =>
        (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString("hh\\:mm", CultureInfo.InvariantCulture);

    private static bool IsHelpToken(string token) =>
        string.Equals(token, "--help", StringComparison.Ordinal) ||
        string.Equals(token, "-h", StringComparison.Ordinal);

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Fetch Microsoft Developer Blogs posts for a date or date range.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  dotnet run --file scripts/fetch_devblog_updates.cs -- <YYYY-MM-DD> [options]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  --to <YYYY-MM-DD>     End date (inclusive). Default: same as the start date.");
        Console.Error.WriteLine("  --tz <±HH:MM>         Timezone used to bucket posts into days. Default: +00:00 (UTC).");
        Console.Error.WriteLine("  --blogs <a,b,c>       Restrict to specific blog slugs (debugging).");
        Console.Error.WriteLine("  --max-pages <N>       Max REST pages per blog. Default: 3.");
        Console.Error.WriteLine("  --concurrency <N>     Parallel blog requests. Default: 8.");
        Console.Error.WriteLine("  --max-description <N> Truncate descriptions to N chars (0 = no limit). Default: 1200.");
        Console.Error.WriteLine("  --diagnostics         Print per-blog fetch results to stderr.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Sources: the aggregate landing feed plus the WordPress REST API of every blog");
        Console.Error.WriteLine("discovered from robots.txt and /wp-json/custom/v1/all-blogs. Results are deduplicated.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Outputs a JSON array to stdout. Each entry has:");
        Console.Error.WriteLine("  title, link, date, published_at, blog, blog_slug, author, description, source");
    }

    private sealed record Options(
        DateOnly From,
        DateOnly To,
        TimeSpan Offset,
        int MaxPages,
        int Concurrency,
        int MaxDescriptionLength,
        bool Diagnostics,
        HashSet<string> OnlySlugs)
    {
        public DateTimeOffset WindowStartUtc =>
            new DateTimeOffset(From.ToDateTime(TimeOnly.MinValue), Offset).ToUniversalTime();

        public DateTimeOffset WindowEndUtc =>
            new DateTimeOffset(To.AddDays(1).ToDateTime(TimeOnly.MinValue), Offset).ToUniversalTime();

        public bool Contains(DateTimeOffset published)
        {
            var utc = published.ToUniversalTime();
            return utc >= WindowStartUtc && utc < WindowEndUtc;
        }

        public string LocalDate(DateTimeOffset published) =>
            published.ToOffset(Offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static Options Parse(string[] args)
        {
            var from = ParseDateArgument(args[0]);
            var to = from;
            var offset = TimeSpan.Zero;
            var maxPages = 3;
            var concurrency = 8;
            var maxDescription = 1200;
            var diagnostics = false;
            var onlySlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var index = 1; index < args.Length; index++)
            {
                var argument = args[index];
                switch (argument)
                {
                    case "--to":
                        to = ParseDateArgument(RequireValue(args, ref index, argument));
                        break;
                    case "--tz":
                        offset = ParseOffset(RequireValue(args, ref index, argument));
                        break;
                    case "--blogs":
                        foreach (var slug in RequireValue(args, ref index, argument)
                                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            onlySlugs.Add(slug);
                        }

                        break;
                    case "--max-pages":
                        maxPages = ParseInt(RequireValue(args, ref index, argument), argument, 1, 50);
                        break;
                    case "--concurrency":
                        concurrency = ParseInt(RequireValue(args, ref index, argument), argument, 1, 32);
                        break;
                    case "--max-description":
                        maxDescription = ParseInt(RequireValue(args, ref index, argument), argument, 0, 100000);
                        break;
                    case "--diagnostics":
                        diagnostics = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{argument}'.");
                }
            }

            if (to < from)
            {
                (from, to) = (to, from);
            }

            return new Options(from, to, offset, maxPages, concurrency, maxDescription, diagnostics, onlySlugs);
        }

        private static string RequireValue(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Option '{option}' requires a value.");
            }

            return args[++index];
        }

        private static DateOnly ParseDateArgument(string value) =>
            DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : throw new ArgumentException($"Invalid date '{value}'. Use YYYY-MM-DD format.");

        private static TimeSpan ParseOffset(string value)
        {
            var text = value.Trim();
            if (text.Equals("UTC", StringComparison.OrdinalIgnoreCase) || text.Equals("Z", StringComparison.OrdinalIgnoreCase))
            {
                return TimeSpan.Zero;
            }

            var match = Regex.Match(text, @"^(?<sign>[+-])?(?<hours>\d{1,2}):?(?<minutes>\d{2})?$");
            if (!match.Success)
            {
                throw new ArgumentException($"Invalid timezone offset '{value}'. Use a form such as +09:00.");
            }

            var hours = int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture);
            var minutes = match.Groups["minutes"].Success && match.Groups["minutes"].Value.Length > 0
                ? int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture)
                : 0;

            var offset = new TimeSpan(hours, minutes, 0);
            if (offset > TimeSpan.FromHours(14))
            {
                throw new ArgumentException($"Timezone offset '{value}' is out of range.");
            }

            return match.Groups["sign"].Value == "-" ? -offset : offset;
        }

        private static int ParseInt(string value, string option, int min, int max) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= min && parsed <= max
                ? parsed
                : throw new ArgumentException($"Option '{option}' requires an integer between {min} and {max}.");
    }

    private sealed record DevBlogEntry(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("link")] string Link,
        [property: JsonPropertyName("date")] string Date,
        [property: JsonPropertyName("published_at")] string PublishedAt,
        [property: JsonPropertyName("blog")] string Blog,
        [property: JsonPropertyName("blog_slug")] string BlogSlug,
        [property: JsonPropertyName("author")] string Author,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("source")] string Source);
}

static class StringExtensions
{
    public static string IfEmpty(this string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
