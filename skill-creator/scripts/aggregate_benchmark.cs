#:property PublishAot=false
#:include SkillCreatorSupport.cs

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

return AggregateBenchmarkCli.Run(args);

static class AggregateBenchmarkCli
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || args.Any(IsHelpToken))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var benchmarkDirectory = Path.GetFullPath(args[0]);
        if (!Directory.Exists(benchmarkDirectory))
        {
            Console.WriteLine($"Directory not found: {benchmarkDirectory}");
            return 1;
        }

        var skillName = string.Empty;
        var skillPath = string.Empty;
        string? outputPath = null;

        for (var index = 1; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--skill-name":
                    skillName = RequireValue(args, ref index, "--skill-name");
                    break;
                case "--skill-path":
                    skillPath = RequireValue(args, ref index, "--skill-path");
                    break;
                case "--output":
                case "-o":
                    outputPath = Path.GetFullPath(RequireValue(args, ref index, args[index]));
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        var benchmark = GenerateBenchmark(benchmarkDirectory, skillName, skillPath);
        outputPath ??= Path.Combine(benchmarkDirectory, "benchmark.json");
        var markdownPath = Path.ChangeExtension(outputPath, ".md");

        File.WriteAllText(outputPath, JsonSerializer.Serialize(benchmark, SkillCreatorSupport.PrettyJson));
        Console.WriteLine($"Generated: {outputPath}");

        var markdown = GenerateMarkdown(benchmark);
        File.WriteAllText(markdownPath, markdown);
        Console.WriteLine($"Generated: {markdownPath}");

        Console.WriteLine();
        Console.WriteLine("Summary:");
        foreach (var config in benchmark.SummarySets.Keys)
        {
            var summary = benchmark.SummarySets[config];
            Console.WriteLine($"  {ToLabel(config)}: {summary.PassRate.Mean * 100:F1}% pass rate");
        }

        Console.WriteLine($"  Delta:         {benchmark.Delta.PassRate}");
        return 0;
    }

    private static BenchmarkDocument GenerateBenchmark(string benchmarkDirectory, string skillName, string skillPath)
    {
        var results = LoadRunResults(benchmarkDirectory);
        var summaries = AggregateResults(results);
        var runSummary = summaries.Summaries.ToDictionary(pair => pair.Key, pair => (object)pair.Value, StringComparer.Ordinal);
        runSummary["delta"] = summaries.Delta;
        var runs = results.SelectMany(pair => pair.Value.Select(result => new BenchmarkRun
        {
            EvalId = result.EvalId,
            Configuration = pair.Key,
            RunNumber = result.RunNumber,
            Result = new BenchmarkRunResult
            {
                PassRate = result.PassRate,
                Passed = result.Passed,
                Failed = result.Failed,
                Total = result.Total,
                TimeSeconds = result.TimeSeconds,
                Tokens = result.Tokens,
                ToolCalls = result.ToolCalls,
                Errors = result.Errors
            },
            Expectations = result.Expectations,
            Notes = result.Notes
        })).ToList();

        var evalIds = results.SelectMany(pair => pair.Value.Select(result => result.EvalId)).Distinct().OrderBy(id => id).ToList();

        return new BenchmarkDocument
        {
            Metadata = new BenchmarkMetadata
            {
                SkillName = string.IsNullOrWhiteSpace(skillName) ? "<skill-name>" : skillName,
                SkillPath = string.IsNullOrWhiteSpace(skillPath) ? "<path/to/skill>" : skillPath,
                ExecutorModel = "<model-name>",
                AnalyzerModel = "<model-name>",
                Timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                EvalsRun = evalIds,
                RunsPerConfiguration = 3
            },
            Runs = runs,
            RunSummary = runSummary,
            SummarySets = summaries.Summaries,
            Delta = summaries.Delta,
            Notes = []
        };
    }

    private static Dictionary<string, List<RunResult>> LoadRunResults(string benchmarkDirectory)
    {
        var directEvalDirectories = Directory.Exists(benchmarkDirectory)
            ? Directory.GetDirectories(benchmarkDirectory, "eval-*", SearchOption.TopDirectoryOnly)
            : [];
        var runsDirectory = Path.Combine(benchmarkDirectory, "runs");
        var searchDirectory = Directory.Exists(runsDirectory)
            ? runsDirectory
            : directEvalDirectories.Length > 0
                ? benchmarkDirectory
                : string.Empty;

        if (string.IsNullOrWhiteSpace(searchDirectory))
        {
            Console.WriteLine($"No eval directories found in {benchmarkDirectory} or {runsDirectory}");
            return new Dictionary<string, List<RunResult>>(StringComparer.Ordinal);
        }

        var results = new Dictionary<string, List<RunResult>>(StringComparer.Ordinal);
        var evalDirectories = Directory.GetDirectories(searchDirectory, "eval-*", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        for (var evalIndex = 0; evalIndex < evalDirectories.Length; evalIndex++)
        {
            var evalDirectory = evalDirectories[evalIndex];
            var evalId = GetEvalId(evalDirectory, evalIndex);

            foreach (var configDirectory in Directory.GetDirectories(evalDirectory).OrderBy(path => path, StringComparer.Ordinal))
            {
                var runDirectories = Directory.GetDirectories(configDirectory, "run-*", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                if (runDirectories.Length == 0)
                {
                    continue;
                }

                var configuration = Path.GetFileName(configDirectory);
                if (!results.ContainsKey(configuration))
                {
                    results[configuration] = [];
                }

                foreach (var runDirectory in runDirectories)
                {
                    var gradingPath = Path.Combine(runDirectory, "grading.json");
                    if (!File.Exists(gradingPath))
                    {
                        Console.WriteLine($"Warning: grading.json not found in {runDirectory}");
                        continue;
                    }

                    JsonDocument gradingDocument;
                    try
                    {
                        gradingDocument = JsonDocument.Parse(File.ReadAllText(gradingPath));
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Warning: Invalid JSON in {gradingPath}: {ex.Message}");
                        continue;
                    }

                    using (gradingDocument)
                    {
                        var grading = gradingDocument.RootElement;
                        var summary = grading.TryGetProperty("summary", out var summaryElement) ? summaryElement : default;
                        var timing = grading.TryGetProperty("timing", out var timingElement) ? timingElement : default;
                        var metrics = grading.TryGetProperty("execution_metrics", out var metricsElement) ? metricsElement : default;

                        var timeSeconds = GetDouble(timing, "total_duration_seconds");
                        var tokens = 0d;
                        var timingPath = Path.Combine(runDirectory, "timing.json");
                        if (timeSeconds == 0.0 && File.Exists(timingPath))
                        {
                            try
                            {
                                using var timingDocument = JsonDocument.Parse(File.ReadAllText(timingPath));
                                timeSeconds = GetDouble(timingDocument.RootElement, "total_duration_seconds");
                                tokens = GetDouble(timingDocument.RootElement, "total_tokens");
                            }
                            catch (JsonException)
                            {
                            }
                        }

                        if (tokens == 0.0)
                        {
                            tokens = GetDouble(metrics, "output_chars");
                        }

                        var expectations = grading.TryGetProperty("expectations", out var expectationsElement) &&
                                           expectationsElement.ValueKind == JsonValueKind.Array
                            ? JsonSerializer.Deserialize<List<JsonElement>>(expectationsElement.GetRawText()) ?? []
                            : [];
                        foreach (var expectation in expectations)
                        {
                            if (!(expectation.TryGetProperty("text", out _) &&
                                  expectation.TryGetProperty("passed", out _) &&
                                  expectation.TryGetProperty("evidence", out _)))
                            {
                                Console.WriteLine($"Warning: expectation in {gradingPath} missing required fields (text, passed, evidence): {expectation.GetRawText()}");
                            }
                        }

                        var notes = new List<string>();
                        if (grading.TryGetProperty("user_notes_summary", out var notesSummary))
                        {
                            AddNotes(notesSummary, "uncertainties", notes);
                            AddNotes(notesSummary, "needs_review", notes);
                            AddNotes(notesSummary, "workarounds", notes);
                        }

                        results[configuration].Add(new RunResult
                        {
                            EvalId = evalId,
                            RunNumber = ParseRunNumber(runDirectory),
                            PassRate = GetDouble(summary, "pass_rate"),
                            Passed = (int)GetDouble(summary, "passed"),
                            Failed = (int)GetDouble(summary, "failed"),
                            Total = (int)GetDouble(summary, "total"),
                            TimeSeconds = timeSeconds,
                            Tokens = tokens,
                            ToolCalls = GetDouble(metrics, "total_tool_calls"),
                            Errors = GetDouble(metrics, "errors_encountered"),
                            Expectations = expectations,
                            Notes = notes
                        });
                    }
                }
            }
        }

        return results;
    }

    private static (Dictionary<string, MetricSummarySet> Summaries, DeltaSummary Delta) AggregateResults(Dictionary<string, List<RunResult>> results)
    {
        var summaries = new Dictionary<string, MetricSummarySet>(StringComparer.Ordinal);
        foreach (var pair in results)
        {
            summaries[pair.Key] = pair.Value.Count == 0
                ? new MetricSummarySet()
                : new MetricSummarySet
                {
                    PassRate = CalculateStats(pair.Value.Select(result => result.PassRate)),
                    TimeSeconds = CalculateStats(pair.Value.Select(result => result.TimeSeconds)),
                    Tokens = CalculateStats(pair.Value.Select(result => result.Tokens))
                };
        }

        var keys = results.Keys.ToList();
        var primary = keys.Count > 0 ? summaries[keys[0]] : new MetricSummarySet();
        var baseline = keys.Count > 1 ? summaries[keys[1]] : new MetricSummarySet();
        var delta = new DeltaSummary
        {
            PassRate = $"{primary.PassRate.Mean - baseline.PassRate.Mean:+0.00;-0.00;+0.00}",
            TimeSeconds = $"{primary.TimeSeconds.Mean - baseline.TimeSeconds.Mean:+0.0;-0.0;+0.0}",
            Tokens = $"{primary.Tokens.Mean - baseline.Tokens.Mean:+0;-0;+0}"
        };

        return (summaries, delta);
    }

    private static MetricSummary CalculateStats(IEnumerable<double> values)
    {
        var items = values.ToList();
        if (items.Count == 0)
        {
            return new MetricSummary();
        }

        var mean = items.Average();
        var stddev = items.Count > 1
            ? Math.Sqrt(items.Sum(value => Math.Pow(value - mean, 2)) / (items.Count - 1))
            : 0.0;

        return new MetricSummary
        {
            Mean = Math.Round(mean, 4),
            Stddev = Math.Round(stddev, 4),
            Min = Math.Round(items.Min(), 4),
            Max = Math.Round(items.Max(), 4)
        };
    }

    private static string GenerateMarkdown(BenchmarkDocument benchmark)
    {
        var summaries = benchmark.SummarySets;
        var configurations = summaries.Keys.ToList();
        var configA = configurations.Count > 0 ? configurations[0] : "config_a";
        var configB = configurations.Count > 1 ? configurations[1] : "config_b";
        var aSummary = summaries.TryGetValue(configA, out var first) ? first : new MetricSummarySet();
        var bSummary = summaries.TryGetValue(configB, out var second) ? second : new MetricSummarySet();

        var lines = new List<string>
        {
            $"# Skill Benchmark: {benchmark.Metadata.SkillName}",
            string.Empty,
            $"**Model**: {benchmark.Metadata.ExecutorModel}",
            $"**Date**: {benchmark.Metadata.Timestamp}",
            $"**Evals**: {string.Join(", ", benchmark.Metadata.EvalsRun)} ({benchmark.Metadata.RunsPerConfiguration} runs each per configuration)",
            string.Empty,
            "## Summary",
            string.Empty,
            $"| Metric | {ToLabel(configA)} | {ToLabel(configB)} | Delta |",
            "|--------|------------|---------------|-------|",
            $"| Pass Rate | {aSummary.PassRate.Mean * 100:F0}% ± {aSummary.PassRate.Stddev * 100:F0}% | {bSummary.PassRate.Mean * 100:F0}% ± {bSummary.PassRate.Stddev * 100:F0}% | {benchmark.Delta.PassRate} |",
            $"| Time | {aSummary.TimeSeconds.Mean:F1}s ± {aSummary.TimeSeconds.Stddev:F1}s | {bSummary.TimeSeconds.Mean:F1}s ± {bSummary.TimeSeconds.Stddev:F1}s | {benchmark.Delta.TimeSeconds}s |",
            $"| Tokens | {aSummary.Tokens.Mean:F0} ± {aSummary.Tokens.Stddev:F0} | {bSummary.Tokens.Mean:F0} ± {bSummary.Tokens.Stddev:F0} | {benchmark.Delta.Tokens} |"
        };

        if (benchmark.Notes.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("## Notes");
            lines.Add(string.Empty);
            lines.AddRange(benchmark.Notes.Select(note => $"- {note}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static int GetEvalId(string evalDirectory, int fallback)
    {
        var metadataPath = Path.Combine(evalDirectory, "eval_metadata.json");
        if (File.Exists(metadataPath))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
                if (document.RootElement.TryGetProperty("eval_id", out var evalIdElement) &&
                    evalIdElement.ValueKind == JsonValueKind.Number &&
                    evalIdElement.TryGetInt32(out var parsedId))
                {
                    return parsedId;
                }
            }
            catch (JsonException)
            {
            }
        }

        var name = Path.GetFileName(evalDirectory);
        return name.StartsWith("eval-", StringComparison.Ordinal) && int.TryParse(name[5..], out var inferred)
            ? inferred
            : fallback;
    }

    private static int ParseRunNumber(string runDirectory)
    {
        var name = Path.GetFileName(runDirectory);
        return name.StartsWith("run-", StringComparison.Ordinal) && int.TryParse(name[4..], out var parsed)
            ? parsed
            : 0;
    }

    private static void AddNotes(JsonElement source, string propertyName, List<string> notes)
    {
        if (source.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.Array)
        {
            notes.AddRange(element.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))!);
        }
    }

    private static double GetDouble(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(propertyName, out var value))
        {
            return 0.0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var number) => number,
            JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var number) => number,
            _ => 0.0
        };
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {option}");
        }

        index++;
        return args[index];
    }

    private static bool IsHelpToken(string value) =>
        value is "-h" or "--help" or "/?";

    private static string ToLabel(string config) =>
        string.Join(' ', config.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => char.ToUpperInvariant(token[0]) + token[1..]));

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --file scripts\\aggregate_benchmark.cs -- <benchmark_dir> [--skill-name <name>] [--skill-path <path>] [-o <output>]");
    }

    private sealed class RunResult
    {
        public int EvalId { get; init; }
        public int RunNumber { get; init; }
        public double PassRate { get; init; }
        public int Passed { get; init; }
        public int Failed { get; init; }
        public int Total { get; init; }
        public double TimeSeconds { get; init; }
        public double Tokens { get; init; }
        public double ToolCalls { get; init; }
        public double Errors { get; init; }
        public List<JsonElement> Expectations { get; init; } = [];
        public List<string> Notes { get; init; } = [];
    }

    private sealed class BenchmarkDocument
    {
        [JsonPropertyName("metadata")]
        public BenchmarkMetadata Metadata { get; init; } = new();

        [JsonPropertyName("runs")]
        public List<BenchmarkRun> Runs { get; init; } = [];

        [JsonPropertyName("run_summary")]
        public Dictionary<string, object> RunSummary { get; init; } = new(StringComparer.Ordinal);

        [JsonIgnore]
        public DeltaSummary Delta { get; init; } = new();

        [JsonIgnore]
        public Dictionary<string, MetricSummarySet> SummarySets { get; init; } = new(StringComparer.Ordinal);

        [JsonPropertyName("notes")]
        public List<string> Notes { get; init; } = [];
    }

    private sealed class BenchmarkMetadata
    {
        [JsonPropertyName("skill_name")]
        public string SkillName { get; init; } = string.Empty;

        [JsonPropertyName("skill_path")]
        public string SkillPath { get; init; } = string.Empty;

        [JsonPropertyName("executor_model")]
        public string ExecutorModel { get; init; } = string.Empty;

        [JsonPropertyName("analyzer_model")]
        public string AnalyzerModel { get; init; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; init; } = string.Empty;

        [JsonPropertyName("evals_run")]
        public List<int> EvalsRun { get; init; } = [];

        [JsonPropertyName("runs_per_configuration")]
        public int RunsPerConfiguration { get; init; }
    }

    private sealed class BenchmarkRun
    {
        [JsonPropertyName("eval_id")]
        public int EvalId { get; init; }

        [JsonPropertyName("configuration")]
        public string Configuration { get; init; } = string.Empty;

        [JsonPropertyName("run_number")]
        public int RunNumber { get; init; }

        [JsonPropertyName("result")]
        public BenchmarkRunResult Result { get; init; } = new();

        [JsonPropertyName("expectations")]
        public List<JsonElement> Expectations { get; init; } = [];

        [JsonPropertyName("notes")]
        public List<string> Notes { get; init; } = [];
    }

    private sealed class BenchmarkRunResult
    {
        [JsonPropertyName("pass_rate")]
        public double PassRate { get; init; }

        [JsonPropertyName("passed")]
        public int Passed { get; init; }

        [JsonPropertyName("failed")]
        public int Failed { get; init; }

        [JsonPropertyName("total")]
        public int Total { get; init; }

        [JsonPropertyName("time_seconds")]
        public double TimeSeconds { get; init; }

        [JsonPropertyName("tokens")]
        public double Tokens { get; init; }

        [JsonPropertyName("tool_calls")]
        public double ToolCalls { get; init; }

        [JsonPropertyName("errors")]
        public double Errors { get; init; }
    }

    private sealed class MetricSummarySet
    {
        [JsonPropertyName("pass_rate")]
        public MetricSummary PassRate { get; init; } = new();

        [JsonPropertyName("time_seconds")]
        public MetricSummary TimeSeconds { get; init; } = new();

        [JsonPropertyName("tokens")]
        public MetricSummary Tokens { get; init; } = new();
    }

    private sealed class MetricSummary
    {
        [JsonPropertyName("mean")]
        public double Mean { get; init; }

        [JsonPropertyName("stddev")]
        public double Stddev { get; init; }

        [JsonPropertyName("min")]
        public double Min { get; init; }

        [JsonPropertyName("max")]
        public double Max { get; init; }
    }

    private sealed class DeltaSummary
    {
        [JsonPropertyName("pass_rate")]
        public string PassRate { get; init; } = "+0.00";

        [JsonPropertyName("time_seconds")]
        public string TimeSeconds { get; init; } = "+0.0";

        [JsonPropertyName("tokens")]
        public string Tokens { get; init; } = "+0";
    }
}
