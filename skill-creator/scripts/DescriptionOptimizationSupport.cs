using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

internal sealed class EvalItem
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("should_trigger")]
    public bool ShouldTrigger { get; set; } = true;
}

internal sealed class EvalResult
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("should_trigger")]
    public bool ShouldTrigger { get; set; }

    [JsonPropertyName("trigger_rate")]
    public double TriggerRate { get; set; }

    [JsonPropertyName("triggers")]
    public int Triggers { get; set; }

    [JsonPropertyName("runs")]
    public int Runs { get; set; }

    [JsonPropertyName("pass")]
    public bool Pass { get; set; }
}

internal sealed class EvalSummary
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("passed")]
    public int Passed { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }
}

internal sealed class EvalResultsDocument
{
    [JsonPropertyName("skill_name")]
    public string SkillName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("results")]
    public List<EvalResult> Results { get; set; } = [];

    [JsonPropertyName("summary")]
    public EvalSummary Summary { get; set; } = new();
}

internal sealed class HistoryEntry
{
    [JsonPropertyName("iteration")]
    public int? Iteration { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("train_passed")]
    public int? TrainPassed { get; set; }

    [JsonPropertyName("train_failed")]
    public int? TrainFailed { get; set; }

    [JsonPropertyName("train_total")]
    public int? TrainTotal { get; set; }

    [JsonPropertyName("train_results")]
    public List<EvalResult>? TrainResults { get; set; }

    [JsonPropertyName("test_passed")]
    public int? TestPassed { get; set; }

    [JsonPropertyName("test_failed")]
    public int? TestFailed { get; set; }

    [JsonPropertyName("test_total")]
    public int? TestTotal { get; set; }

    [JsonPropertyName("test_results")]
    public List<EvalResult>? TestResults { get; set; }

    [JsonPropertyName("passed")]
    public int? Passed { get; set; }

    [JsonPropertyName("failed")]
    public int? Failed { get; set; }

    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("results")]
    public List<EvalResult>? Results { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

internal sealed class LoopOutput
{
    [JsonPropertyName("exit_reason")]
    public string ExitReason { get; set; } = string.Empty;

    [JsonPropertyName("original_description")]
    public string OriginalDescription { get; set; } = string.Empty;

    [JsonPropertyName("best_description")]
    public string BestDescription { get; set; } = string.Empty;

    [JsonPropertyName("best_score")]
    public string BestScore { get; set; } = string.Empty;

    [JsonPropertyName("best_train_score")]
    public string BestTrainScore { get; set; } = string.Empty;

    [JsonPropertyName("best_test_score")]
    public string? BestTestScore { get; set; }

    [JsonPropertyName("final_description")]
    public string FinalDescription { get; set; } = string.Empty;

    [JsonPropertyName("iterations_run")]
    public int IterationsRun { get; set; }

    [JsonPropertyName("holdout")]
    public double Holdout { get; set; }

    [JsonPropertyName("train_size")]
    public int TrainSize { get; set; }

    [JsonPropertyName("test_size")]
    public int TestSize { get; set; }

    [JsonPropertyName("history")]
    public List<HistoryEntry> History { get; set; } = [];
}

internal static class DescriptionOptimizationSupport
{
    private static readonly Regex NewDescriptionRegex =
        new("<new_description>(.*?)</new_description>", RegexOptions.Singleline | RegexOptions.Compiled);

    public static List<EvalItem> LoadEvalSet(string path) =>
        JsonSerializer.Deserialize<List<EvalItem>>(File.ReadAllText(path)) ?? [];

    public static List<HistoryEntry> LoadHistory(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => JsonSerializer.Deserialize<List<HistoryEntry>>(document.RootElement.GetRawText()) ?? [],
            JsonValueKind.Object when document.RootElement.TryGetProperty("history", out var historyElement) =>
                JsonSerializer.Deserialize<List<HistoryEntry>>(historyElement.GetRawText()) ?? [],
            _ => []
        };
    }

    public static string FindProjectRoot()
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".claude")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Environment.CurrentDirectory;
    }

    public static async Task<EvalResultsDocument> RunEvalAsync(
        IReadOnlyList<EvalItem> evalSet,
        string skillName,
        string description,
        int numWorkers,
        int timeoutSeconds,
        string projectRoot,
        int runsPerQuery,
        double triggerThreshold,
        string? model)
    {
        var semaphore = new SemaphoreSlim(Math.Max(1, numWorkers));
        var tasks = new List<Task<(EvalItem Item, bool Triggered)>>();

        foreach (var item in evalSet)
        {
            for (var runIndex = 0; runIndex < runsPerQuery; runIndex++)
            {
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var triggered = await RunSingleQueryAsync(item.Query, skillName, description, timeoutSeconds, projectRoot, model);
                        return (item, triggered);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Warning: query failed: {ex.Message}");
                        return (item, false);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }
        }

        var grouped = new Dictionary<string, (EvalItem Item, List<bool> Runs)>(StringComparer.Ordinal);
        foreach (var (item, triggered) in await Task.WhenAll(tasks))
        {
            if (!grouped.TryGetValue(item.Query, out var entry))
            {
                entry = (item, []);
            }

            entry.Runs.Add(triggered);
            grouped[item.Query] = entry;
        }

        var results = grouped.Values
            .Select(entry =>
            {
                var triggers = entry.Runs.Count(result => result);
                var triggerRate = entry.Runs.Count == 0 ? 0.0 : triggers / (double)entry.Runs.Count;
                var passed = entry.Item.ShouldTrigger
                    ? triggerRate >= triggerThreshold
                    : triggerRate < triggerThreshold;

                return new EvalResult
                {
                    Query = entry.Item.Query,
                    ShouldTrigger = entry.Item.ShouldTrigger,
                    TriggerRate = triggerRate,
                    Triggers = triggers,
                    Runs = entry.Runs.Count,
                    Pass = passed
                };
            })
            .OrderBy(result => result.Query, StringComparer.Ordinal)
            .ToList();

        var passedCount = results.Count(result => result.Pass);
        return new EvalResultsDocument
        {
            SkillName = skillName,
            Description = description,
            Results = results,
            Summary = new EvalSummary
            {
                Total = results.Count,
                Passed = passedCount,
                Failed = results.Count - passedCount
            }
        };
    }

    public static async Task<string> ImproveDescriptionAsync(
        string skillName,
        string skillContent,
        string currentDescription,
        EvalResultsDocument evalResults,
        IReadOnlyList<HistoryEntry> history,
        string model,
        EvalResultsDocument? testResults = null,
        string? logDirectory = null,
        int? iteration = null)
    {
        var failedTriggers = evalResults.Results.Where(result => result.ShouldTrigger && !result.Pass).ToList();
        var falseTriggers = evalResults.Results.Where(result => !result.ShouldTrigger && !result.Pass).ToList();
        var trainScore = $"{evalResults.Summary.Passed}/{evalResults.Summary.Total}";
        var scoreSummary = testResults is null
            ? $"Train: {trainScore}"
            : $"Train: {trainScore}, Test: {testResults.Summary.Passed}/{testResults.Summary.Total}";

        var prompt = new StringBuilder();
        prompt.AppendLine($"You are optimizing a skill description for a Claude Code skill called \"{skillName}\". A \"skill\" is sort of like a prompt, but with progressive disclosure -- there's a title and description that Claude sees when deciding whether to use the skill, and then if it does use the skill, it reads the .md file which has lots more details and potentially links to other resources in the skill folder like helper files and scripts and additional documentation or examples.");
        prompt.AppendLine();
        prompt.AppendLine("The description appears in Claude's \"available_skills\" list. When a user sends a query, Claude decides whether to invoke the skill based solely on the title and on this description. Your goal is to write a description that triggers for relevant queries, and doesn't trigger for irrelevant ones.");
        prompt.AppendLine();
        prompt.AppendLine("Here's the current description:");
        prompt.AppendLine("<current_description>");
        prompt.AppendLine($"\"{currentDescription}\"");
        prompt.AppendLine("</current_description>");
        prompt.AppendLine();
        prompt.AppendLine($"Current scores ({scoreSummary}):");
        prompt.AppendLine("<scores_summary>");

        if (failedTriggers.Count > 0)
        {
            prompt.AppendLine("FAILED TO TRIGGER (should have triggered but didn't):");
            foreach (var result in failedTriggers)
            {
                prompt.AppendLine($"  - \"{result.Query}\" (triggered {result.Triggers}/{result.Runs} times)");
            }

            prompt.AppendLine();
        }

        if (falseTriggers.Count > 0)
        {
            prompt.AppendLine("FALSE TRIGGERS (triggered but shouldn't have):");
            foreach (var result in falseTriggers)
            {
                prompt.AppendLine($"  - \"{result.Query}\" (triggered {result.Triggers}/{result.Runs} times)");
            }

            prompt.AppendLine();
        }

        if (history.Count > 0)
        {
            prompt.AppendLine("PREVIOUS ATTEMPTS (do NOT repeat these — try something structurally different):");
            prompt.AppendLine();
            foreach (var entry in history)
            {
                var train = $"{entry.TrainPassed ?? entry.Passed ?? 0}/{entry.TrainTotal ?? entry.Total ?? 0}";
                var score = entry.TestPassed is null
                    ? $"train={train}"
                    : $"train={train}, test={entry.TestPassed}/{entry.TestTotal}";
                prompt.AppendLine($"<attempt {score}>");
                prompt.AppendLine($"Description: \"{entry.Description}\"");

                if (entry.Results is { Count: > 0 })
                {
                    prompt.AppendLine("Train results:");
                    foreach (var result in entry.Results)
                    {
                        var status = result.Pass ? "PASS" : "FAIL";
                        var preview = result.Query.Length <= 80 ? result.Query : result.Query[..80];
                        prompt.AppendLine($"  [{status}] \"{preview}\" (triggered {result.Triggers}/{result.Runs})");
                    }
                }

                if (!string.IsNullOrWhiteSpace(entry.Note))
                {
                    prompt.AppendLine($"Note: {entry.Note}");
                }

                prompt.AppendLine("</attempt>");
                prompt.AppendLine();
            }
        }

        prompt.AppendLine("</scores_summary>");
        prompt.AppendLine();
        prompt.AppendLine("Skill content (for context on what the skill does):");
        prompt.AppendLine("<skill_content>");
        prompt.AppendLine(skillContent);
        prompt.AppendLine("</skill_content>");
        prompt.AppendLine();
        prompt.AppendLine("Based on the failures, write a new and improved description that is more likely to trigger correctly. When I say \"based on the failures\", it's a bit of a tricky line to walk because we don't want to overfit to the specific cases you're seeing. So what I DON'T want you to do is produce an ever-expanding list of specific queries that this skill should or shouldn't trigger for. Instead, try to generalize from the failures to broader categories of user intent and situations where this skill would be useful or not useful. The reason for this is twofold:");
        prompt.AppendLine();
        prompt.AppendLine("1. Avoid overfitting");
        prompt.AppendLine("2. The list might get loooong and it's injected into ALL queries and there might be a lot of skills, so we don't want to blow too much space on any given description.");
        prompt.AppendLine();
        prompt.AppendLine("Concretely, your description should not be more than about 100-200 words, even if that comes at the cost of accuracy. There is a hard limit of 1024 characters — descriptions over that will be truncated, so stay comfortably under it.");
        prompt.AppendLine();
        prompt.AppendLine("Here are some tips that we've found to work well in writing these descriptions:");
        prompt.AppendLine("- The skill should be phrased in the imperative -- \"Use this skill for\" rather than \"this skill does\"");
        prompt.AppendLine("- The skill description should focus on the user's intent, what they are trying to achieve, vs. the implementation details of how the skill works.");
        prompt.AppendLine("- The description competes with other skills for Claude's attention — make it distinctive and immediately recognizable.");
        prompt.AppendLine("- If you're getting lots of failures after repeated attempts, change things up. Try different sentence structures or wordings.");
        prompt.AppendLine();
        prompt.AppendLine("I'd encourage you to be creative and mix up the style in different iterations since you'll have multiple opportunities to try different approaches and we'll just grab the highest-scoring one at the end.");
        prompt.AppendLine();
        prompt.AppendLine("Please respond with only the new description text in <new_description> tags, nothing else.");

        var transcript = new Dictionary<string, object?>
        {
            ["iteration"] = iteration,
            ["prompt"] = prompt.ToString()
        };

        var response = await CallClaudeAsync(prompt.ToString(), model, 300);
        var description = ExtractDescription(response);
        transcript["response"] = response;
        transcript["parsed_description"] = description;
        transcript["char_count"] = description.Length;
        transcript["over_limit"] = description.Length > 1024;

        if (description.Length > 1024)
        {
            var shortenPrompt = $"""
{prompt}

---

A previous attempt produced this description, which at {description.Length} characters is over the 1024-character hard limit:

"{description}"

Rewrite it to be under 1024 characters while keeping the most important trigger words and intent coverage. Respond with only the new description in <new_description> tags.
""";

            var shortenedResponse = await CallClaudeAsync(shortenPrompt, model, 300);
            var shortenedDescription = ExtractDescription(shortenedResponse);
            transcript["rewrite_prompt"] = shortenPrompt;
            transcript["rewrite_response"] = shortenedResponse;
            transcript["rewrite_description"] = shortenedDescription;
            transcript["rewrite_char_count"] = shortenedDescription.Length;
            description = shortenedDescription;
        }

        transcript["final_description"] = description;

        if (!string.IsNullOrWhiteSpace(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
            var fileName = $"improve_iter_{iteration?.ToString() ?? "unknown"}.json";
            var logPath = Path.Combine(logDirectory, fileName);
            File.WriteAllText(logPath, JsonSerializer.Serialize(transcript, SkillCreatorSupport.PrettyJson));
        }

        return description;
    }

    public static (List<EvalItem> Train, List<EvalItem> Test) SplitEvalSet(IReadOnlyList<EvalItem> evalSet, double holdout, int seed = 42)
    {
        var random = new Random(seed);
        var trigger = evalSet.Where(item => item.ShouldTrigger).ToList();
        var noTrigger = evalSet.Where(item => !item.ShouldTrigger).ToList();
        Shuffle(trigger, random);
        Shuffle(noTrigger, random);

        var triggerTestCount = Math.Max(1, (int)(trigger.Count * holdout));
        var noTriggerTestCount = Math.Max(1, (int)(noTrigger.Count * holdout));

        var test = trigger.Take(triggerTestCount).Concat(noTrigger.Take(noTriggerTestCount)).ToList();
        var train = trigger.Skip(triggerTestCount).Concat(noTrigger.Skip(noTriggerTestCount)).ToList();
        return (train, test);
    }

    private static async Task<bool> RunSingleQueryAsync(
        string query,
        string skillName,
        string skillDescription,
        int timeoutSeconds,
        string projectRoot,
        string? model)
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var cleanName = $"{skillName}-skill-{uniqueId}";
        var commandsDirectory = Path.Combine(projectRoot, ".claude", "commands");
        Directory.CreateDirectory(commandsDirectory);

        var commandPath = Path.Combine(commandsDirectory, $"{cleanName}.md");
        try
        {
            var indentedDescription = string.Join("\n  ", SkillCreatorSupport.NormalizeNewlines(skillDescription).Split('\n'));
            var commandContent = $"""
---
description: |
  {indentedDescription}
---

# {skillName}

This skill handles: {skillDescription}
""";
            File.WriteAllText(commandPath, commandContent);

            var startInfo = new ProcessStartInfo
            {
                FileName = "claude",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = projectRoot
            };
            startInfo.ArgumentList.Add("-p");
            startInfo.ArgumentList.Add(query);
            startInfo.ArgumentList.Add("--output-format");
            startInfo.ArgumentList.Add("stream-json");
            startInfo.ArgumentList.Add("--verbose");
            startInfo.ArgumentList.Add("--include-partial-messages");
            if (!string.IsNullOrWhiteSpace(model))
            {
                startInfo.ArgumentList.Add("--model");
                startInfo.ArgumentList.Add(model);
            }

            startInfo.Environment.Remove("CLAUDECODE");

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start claude.");

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            string? pendingToolName = null;
            var accumulatedJson = new StringBuilder();
            var triggered = false;

            var outputTask = Task.Run(async () =>
            {
                while (await process.StandardOutput.ReadLineAsync() is { } line)
                {
                    if (!TryProcessStreamLine(line, cleanName, ref pendingToolName, accumulatedJson, ref triggered, out var result))
                    {
                        continue;
                    }

                    completion.TrySetResult(result);
                    return;
                }

                completion.TrySetResult(triggered);
            });

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var registration = timeoutCts.Token.Register(() => completion.TrySetCanceled(timeoutCts.Token));

            try
            {
                var result = await completion.Task.WaitAsync(timeoutCts.Token);
                TryKill(process);
                await outputTask;
                return result;
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return false;
            }
            finally
            {
                try
                {
                    await process.WaitForExitAsync();
                }
                catch
                {
                }
            }
        }
        finally
        {
            if (File.Exists(commandPath))
            {
                File.Delete(commandPath);
            }
        }
    }

    private static bool TryProcessStreamLine(
        string line,
        string cleanName,
        ref string? pendingToolName,
        StringBuilder accumulatedJson,
        ref bool triggered,
        out bool result)
    {
        result = false;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeElement))
            {
                return false;
            }

            var type = typeElement.GetString();
            if (string.Equals(type, "stream_event", StringComparison.Ordinal))
            {
                if (!root.TryGetProperty("event", out var eventElement) ||
                    !eventElement.TryGetProperty("type", out var streamTypeElement))
                {
                    return false;
                }

                var streamType = streamTypeElement.GetString();
                switch (streamType)
                {
                    case "content_block_start":
                        if (eventElement.TryGetProperty("content_block", out var contentBlock) &&
                            contentBlock.TryGetProperty("type", out var contentType) &&
                            string.Equals(contentType.GetString(), "tool_use", StringComparison.Ordinal))
                        {
                            var toolName = contentBlock.TryGetProperty("name", out var toolNameElement)
                                ? toolNameElement.GetString()
                                : null;
                            if (toolName is "Skill" or "Read")
                            {
                                pendingToolName = toolName;
                                accumulatedJson.Clear();
                            }
                            else
                            {
                                result = false;
                                return true;
                            }
                        }

                        return false;

                    case "content_block_delta":
                        if (pendingToolName is not null &&
                            eventElement.TryGetProperty("delta", out var deltaElement) &&
                            deltaElement.TryGetProperty("type", out var deltaTypeElement) &&
                            string.Equals(deltaTypeElement.GetString(), "input_json_delta", StringComparison.Ordinal) &&
                            deltaElement.TryGetProperty("partial_json", out var partialJsonElement))
                        {
                            accumulatedJson.Append(partialJsonElement.GetString());
                            if (accumulatedJson.ToString().Contains(cleanName, StringComparison.Ordinal))
                            {
                                result = true;
                                return true;
                            }
                        }

                        return false;

                    case "content_block_stop":
                    case "message_stop":
                        if (pendingToolName is not null)
                        {
                            result = accumulatedJson.ToString().Contains(cleanName, StringComparison.Ordinal);
                            return true;
                        }

                        if (string.Equals(streamType, "message_stop", StringComparison.Ordinal))
                        {
                            result = false;
                            return true;
                        }

                        return false;
                }

                return false;
            }

            if (string.Equals(type, "assistant", StringComparison.Ordinal) &&
                root.TryGetProperty("message", out var messageElement) &&
                messageElement.TryGetProperty("content", out var contentArray) &&
                contentArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in contentArray.EnumerateArray())
                {
                    if (!item.TryGetProperty("type", out var itemType) ||
                        !string.Equals(itemType.GetString(), "tool_use", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var toolName = item.TryGetProperty("name", out var toolNameElement)
                        ? toolNameElement.GetString()
                        : null;

                    if (!item.TryGetProperty("input", out var inputElement))
                    {
                        continue;
                    }

                    if (toolName == "Skill" &&
                        inputElement.TryGetProperty("skill", out var skillElement) &&
                        skillElement.GetString()?.Contains(cleanName, StringComparison.Ordinal) == true)
                    {
                        triggered = true;
                    }
                    else if (toolName == "Read" &&
                        inputElement.TryGetProperty("file_path", out var filePathElement) &&
                        filePathElement.GetString()?.Contains(cleanName, StringComparison.Ordinal) == true)
                    {
                        triggered = true;
                    }

                    result = triggered;
                    return true;
                }

                return false;
            }

            if (string.Equals(type, "result", StringComparison.Ordinal))
            {
                result = triggered;
                return true;
            }

            return false;
        }
    }

    private static async Task<string> CallClaudeAsync(string prompt, string? model, int timeoutSeconds)
    {
        var arguments = new List<string> { "-p", "--output-format", "text" };
        if (!string.IsNullOrWhiteSpace(model))
        {
            arguments.Add("--model");
            arguments.Add(model);
        }

        var result = await SkillCreatorSupport.RunProcessAsync(
            "claude",
            arguments,
            standardInput: prompt,
            timeoutSeconds: timeoutSeconds,
            removeClaudeCodeEnvironment: true);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"claude -p exited {result.ExitCode}{Environment.NewLine}stderr: {result.StandardError}");
        }

        return result.StandardOutput;
    }

    private static string ExtractDescription(string response)
    {
        var match = NewDescriptionRegex.Match(response);
        return match.Success
            ? match.Groups[1].Value.Trim().Trim('"')
            : response.Trim().Trim('"');
    }

    private static void Shuffle<T>(IList<T> items, Random random)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
