#!/usr/bin/env dotnet
#:property PublishAot=false

using System.Net.Http.Headers;
using System.Text;
using System.Xml;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: dotnet hatena-client.cs -- <file-path>");
    return 1;
}

var filePath = args[0];
if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"File not found: {filePath}");
    return 1;
}

var fileContent = await File.ReadAllTextAsync(filePath);
var firstLine   = fileContent.Split('\n', 2)[0].TrimStart('#').Trim();

var username = Environment.GetEnvironmentVariable("HATENA_USERNAME") ?? "your-username";
var apiKey   = Environment.GetEnvironmentVariable("HATENA_API_KEY")  ?? "your-api-key";
var blogId   = Environment.GetEnvironmentVariable("HATENA_BLOG_ID")  ?? "your-blog.hatenablog.com";

var client = new HatenaClient(username, apiKey, blogId);

var response = await client.PostEntryAsync(
    username: username,
    title: firstLine,
    content: fileContent,
    draft: true
);

Console.WriteLine($"Status: {(int)response.StatusCode} {response.StatusCode}");
Console.WriteLine(await response.Content.ReadAsStringAsync());
return 0;

// ── HatenaClient ──────────────────────────────────────────────────────────────

class HatenaClient(string username, string apiKey, string blogId)
{
    private readonly string _baseUrl =
        $"https://blog.hatena.ne.jp/{username}/{blogId}/atom/entry";

    private readonly HttpClient _http = CreateHttpClient(username, apiKey);

    public async Task<HttpResponseMessage> PostEntryAsync(
        string username,
        string title,
        string content,
        IEnumerable<string>? categories = null,
        bool draft = true,
        string contentType = "text/markdown")
    {
        var xml = BuildEntryXml(username, title, content, categories, draft, contentType);
        Console.WriteLine("Request XML:");
        Console.WriteLine(xml);
        var body = new StringContent(xml, Encoding.UTF8, "application/atom+xml");
        return await _http.PostAsync(_baseUrl, body);
    }

    private static string BuildEntryXml(
        string username,
        string title,
        string content,
        IEnumerable<string>? categories,
        bool draft,
        string contentType)
    {
        var sb = new Utf8StringWriter();
        var settings = new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true };

        using var writer = XmlWriter.Create(sb, settings);
        writer.WriteStartDocument();
        writer.WriteStartElement("entry", "http://www.w3.org/2005/Atom");
        writer.WriteAttributeString("xmlns", "app", null, "http://www.w3.org/2007/app");
        writer.WriteAttributeString("xmlns", "hatenablog", null, "http://www.hatena.ne.jp/info/xmlns#hatenablog");

        writer.WriteElementString("title", title);
        writer.WriteStartElement("author");
        writer.WriteElementString("name", username);
        writer.WriteEndElement();

        if (categories != null)
        {
            foreach (var cat in categories)
            {
                writer.WriteStartElement("category");
                writer.WriteAttributeString("term", cat);
                writer.WriteEndElement();
            }
        }

        writer.WriteStartElement("content");
        writer.WriteAttributeString("type", contentType);
        writer.WriteString(content);
        writer.WriteEndElement();

        if (draft)
        {
            writer.WriteStartElement("control", "http://www.w3.org/2007/app");
            writer.WriteElementString("draft", "http://www.w3.org/2007/app", "yes");
            writer.WriteEndElement();
        }

        writer.WriteEndElement(); // entry
        writer.WriteEndDocument();
        writer.Flush();

        return sb.ToString();
    }

    private static HttpClient CreateHttpClient(string username, string apiKey)
    {
        var http = new HttpClient();
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{apiKey}"));
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", token);
        return http;
    }

    sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
