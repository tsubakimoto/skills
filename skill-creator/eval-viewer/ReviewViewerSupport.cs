using System.Net;
using System.Net.Mime;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

internal sealed class ViewerRun
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("eval_id")]
    public int? EvalId { get; set; }

    [JsonPropertyName("outputs")]
    public List<EmbeddedOutputFile> Outputs { get; set; } = [];

    [JsonPropertyName("grading")]
    public JsonElement? Grading { get; set; }
}

internal sealed class EmbeddedOutputFile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("mime")]
    public string? Mime { get; set; }

    [JsonPropertyName("data_uri")]
    public string? DataUri { get; set; }

    [JsonPropertyName("data_b64")]
    public string? DataBase64 { get; set; }
}

internal sealed class PreviousRunData
{
    public string Feedback { get; set; } = string.Empty;

    public List<EmbeddedOutputFile> Outputs { get; set; } = [];
}

internal static class ReviewViewerSupport
{
    private static readonly HashSet<string> MetadataFiles = ["transcript.md", "user_notes.md", "metrics.json"];

    private static readonly HashSet<string> TextExtensions =
    [
        ".txt", ".md", ".json", ".csv", ".py", ".cs", ".js", ".ts", ".tsx", ".jsx",
        ".yaml", ".yml", ".xml", ".html", ".css", ".sh", ".rb", ".go", ".rs",
        ".java", ".c", ".cpp", ".h", ".hpp", ".sql", ".r", ".toml"
    ];

    private static readonly HashSet<string> ImageExtensions =
    [
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp"
    ];

    private static readonly Dictionary<string, string> MimeOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        [".svg"] = "image/svg+xml",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
    };

    private static readonly Regex PromptRegex =
        new(@"## Eval Prompt\r?\n\r?\n([\s\S]*?)(?=\r?\n##|$)", RegexOptions.Compiled);

    public static List<ViewerRun> FindRuns(string workspacePath)
    {
        var root = new DirectoryInfo(workspacePath);
        var runs = new List<ViewerRun>();
        FindRunsRecursive(root, root, runs);

        return runs
            .OrderBy(run => run.EvalId ?? int.MaxValue)
            .ThenBy(run => run.Id, StringComparer.Ordinal)
            .ToList();
    }

    public static Dictionary<string, PreviousRunData> LoadPreviousIteration(string workspacePath)
    {
        var result = new Dictionary<string, PreviousRunData>(StringComparer.Ordinal);
        var feedbackPath = Path.Combine(workspacePath, "feedback.json");
        Dictionary<string, string> feedback = new(StringComparer.Ordinal);

        if (File.Exists(feedbackPath))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(feedbackPath));
                if (document.RootElement.TryGetProperty("reviews", out var reviews) && reviews.ValueKind == JsonValueKind.Array)
                {
                    foreach (var review in reviews.EnumerateArray())
                    {
                        if (!review.TryGetProperty("run_id", out var runIdElement) ||
                            !review.TryGetProperty("feedback", out var feedbackElement))
                        {
                            continue;
                        }

                        var runId = runIdElement.GetString();
                        var reviewText = feedbackElement.GetString();
                        if (!string.IsNullOrWhiteSpace(runId) && !string.IsNullOrWhiteSpace(reviewText))
                        {
                            feedback[runId] = reviewText;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        foreach (var run in FindRuns(workspacePath))
        {
            result[run.Id] = new PreviousRunData
            {
                Feedback = feedback.TryGetValue(run.Id, out var review) ? review : string.Empty,
                Outputs = run.Outputs
            };
        }

        foreach (var pair in feedback)
        {
            if (!result.ContainsKey(pair.Key))
            {
                result[pair.Key] = new PreviousRunData
                {
                    Feedback = pair.Value
                };
            }
        }

        return result;
    }

    public static string GenerateHtml(
        IReadOnlyList<ViewerRun> runs,
        string skillName,
        IReadOnlyDictionary<string, PreviousRunData>? previous = null,
        JsonElement? benchmark = null)
    {
        var templatePath = Path.Combine(SkillCreatorSupport.GetSourceDirectory(), "viewer.html");
        var template = File.ReadAllText(templatePath);

        var previousFeedback = new Dictionary<string, string>(StringComparer.Ordinal);
        var previousOutputs = new Dictionary<string, List<EmbeddedOutputFile>>(StringComparer.Ordinal);

        if (previous is not null)
        {
            foreach (var pair in previous)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value.Feedback))
                {
                    previousFeedback[pair.Key] = pair.Value.Feedback;
                }

                if (pair.Value.Outputs.Count > 0)
                {
                    previousOutputs[pair.Key] = pair.Value.Outputs;
                }
            }
        }

        var payload = new Dictionary<string, object?>
        {
            ["skill_name"] = skillName,
            ["runs"] = runs,
            ["previous_feedback"] = previousFeedback,
            ["previous_outputs"] = previousOutputs
        };

        if (benchmark is not null)
        {
            payload["benchmark"] = benchmark.Value;
        }

        var json = JsonSerializer.Serialize(payload, SkillCreatorSupport.PrettyJson);
        return template.Replace("/*__EMBEDDED_DATA__*/", $"const EMBEDDED_DATA = {json};", StringComparison.Ordinal);
    }

    public static string GetMimeType(string path)
    {
        var extension = Path.GetExtension(path);
        if (MimeOverrides.TryGetValue(extension, out var overrideMime))
        {
            return overrideMime;
        }

        return extension.ToLowerInvariant() switch
        {
            ".txt" => MediaTypeNames.Text.Plain,
            ".json" => MediaTypeNames.Application.Json,
            ".html" => MediaTypeNames.Text.Html,
            ".csv" => "text/csv",
            ".pdf" => MediaTypeNames.Application.Pdf,
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => MediaTypeNames.Application.Octet
        };
    }

    public static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void FindRunsRecursive(DirectoryInfo root, DirectoryInfo current, List<ViewerRun> runs)
    {
        if (!current.Exists)
        {
            return;
        }

        var outputsDirectory = current.GetDirectories("outputs").FirstOrDefault();
        if (outputsDirectory is not null)
        {
            var run = BuildRun(root, current);
            if (run is not null)
            {
                runs.Add(run);
            }

            return;
        }

        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules",
            ".git",
            "__pycache__",
            "skill",
            "inputs"
        };

        foreach (var child in current.GetDirectories().OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!skip.Contains(child.Name))
            {
                FindRunsRecursive(root, child, runs);
            }
        }
    }

    private static ViewerRun? BuildRun(DirectoryInfo root, DirectoryInfo runDirectory)
    {
        var prompt = string.Empty;
        int? evalId = null;

        foreach (var candidate in new[]
        {
            Path.Combine(runDirectory.FullName, "eval_metadata.json"),
            Path.Combine(runDirectory.Parent?.FullName ?? string.Empty, "eval_metadata.json")
        }.Where(File.Exists))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(candidate));
                if (document.RootElement.TryGetProperty("prompt", out var promptElement))
                {
                    prompt = promptElement.GetString() ?? string.Empty;
                }

                if (document.RootElement.TryGetProperty("eval_id", out var evalIdElement) &&
                    evalIdElement.ValueKind == JsonValueKind.Number &&
                    evalIdElement.TryGetInt32(out var parsedId))
                {
                    evalId = parsedId;
                }

                if (!string.IsNullOrWhiteSpace(prompt))
                {
                    break;
                }
            }
            catch
            {
            }
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            foreach (var candidate in new[]
            {
                Path.Combine(runDirectory.FullName, "transcript.md"),
                Path.Combine(runDirectory.FullName, "outputs", "transcript.md")
            }.Where(File.Exists))
            {
                try
                {
                    var text = File.ReadAllText(candidate);
                    var match = PromptRegex.Match(text);
                    if (match.Success)
                    {
                        prompt = match.Groups[1].Value.Trim();
                        break;
                    }
                }
                catch
                {
                }
            }
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            prompt = "(No prompt found)";
        }

        var runId = Path.GetRelativePath(root.FullName, runDirectory.FullName).Replace('/', '-').Replace('\\', '-');
        var outputs = new List<EmbeddedOutputFile>();
        var outputsPath = Path.Combine(runDirectory.FullName, "outputs");
        if (Directory.Exists(outputsPath))
        {
            foreach (var file in new DirectoryInfo(outputsPath).GetFiles().OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!MetadataFiles.Contains(file.Name))
                {
                    outputs.Add(EmbedFile(file.FullName));
                }
            }
        }

        JsonElement? grading = null;
        foreach (var candidate in new[]
        {
            Path.Combine(runDirectory.FullName, "grading.json"),
            Path.Combine(runDirectory.Parent?.FullName ?? string.Empty, "grading.json")
        }.Where(File.Exists))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(candidate));
                grading = document.RootElement.Clone();
                break;
            }
            catch
            {
            }
        }

        return new ViewerRun
        {
            Id = runId,
            Prompt = prompt,
            EvalId = evalId,
            Outputs = outputs,
            Grading = grading
        };
    }

    private static EmbeddedOutputFile EmbedFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var mime = GetMimeType(path);

        if (TextExtensions.Contains(extension))
        {
            return new EmbeddedOutputFile
            {
                Name = Path.GetFileName(path),
                Type = "text",
                Content = SafeReadText(path)
            };
        }

        var bytes = SafeReadBytes(path);
        if (bytes is null)
        {
            return new EmbeddedOutputFile
            {
                Name = Path.GetFileName(path),
                Type = "error",
                Content = "(Error reading file)"
            };
        }

        var base64 = Convert.ToBase64String(bytes);
        return extension switch
        {
            ".pdf" => new EmbeddedOutputFile
            {
                Name = Path.GetFileName(path),
                Type = "pdf",
                DataUri = $"data:{mime};base64,{base64}"
            },
            ".xlsx" => new EmbeddedOutputFile
            {
                Name = Path.GetFileName(path),
                Type = "xlsx",
                DataBase64 = base64
            },
            _ when ImageExtensions.Contains(extension) => new EmbeddedOutputFile
            {
                Name = Path.GetFileName(path),
                Type = "image",
                Mime = mime,
                DataUri = $"data:{mime};base64,{base64}"
            },
            _ => new EmbeddedOutputFile
            {
                Name = Path.GetFileName(path),
                Type = "binary",
                Mime = mime,
                DataUri = $"data:{mime};base64,{base64}"
            }
        };
    }

    private static byte[]? SafeReadBytes(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch
        {
            return null;
        }
    }

    private static string SafeReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch
        {
            return "(Error reading file)";
        }
    }
}
