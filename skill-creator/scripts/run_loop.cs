#:property PublishAot=false
#:include SkillCreatorSupport.cs
#:include DescriptionOptimizationSupport.cs
#:include ReportSupport.cs

using System.Text.Json;

return await RunLoopCli.RunAsync(args);

static class RunLoopCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args.Any(IsHelpToken))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        string? evalSetPath = null;
        string? skillPath = null;
        string? descriptionOverride = null;
        var numWorkers = 10;
        var timeout = 30;
        var maxIterations = 5;
        var runsPerQuery = 3;
        var triggerThreshold = 0.5;
        var holdout = 0.4;
        string? model = null;
        var verbose = false;
        var report = "auto";
        string? resultsDirectory = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--eval-set":
                    evalSetPath = RequireValue(args, ref index, "--eval-set");
                    break;
                case "--skill-path":
                    skillPath = RequireValue(args, ref index, "--skill-path");
                    break;
                case "--description":
                    descriptionOverride = RequireValue(args, ref index, "--description");
                    break;
                case "--num-workers":
                    numWorkers = int.Parse(RequireValue(args, ref index, "--num-workers"));
                    break;
                case "--timeout":
                    timeout = int.Parse(RequireValue(args, ref index, "--timeout"));
                    break;
                case "--max-iterations":
                    maxIterations = int.Parse(RequireValue(args, ref index, "--max-iterations"));
                    break;
                case "--runs-per-query":
                    runsPerQuery = int.Parse(RequireValue(args, ref index, "--runs-per-query"));
                    break;
                case "--trigger-threshold":
                    triggerThreshold = double.Parse(RequireValue(args, ref index, "--trigger-threshold"));
                    break;
                case "--holdout":
                    holdout = double.Parse(RequireValue(args, ref index, "--holdout"));
                    break;
                case "--model":
                    model = RequireValue(args, ref index, "--model");
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--report":
                    report = RequireValue(args, ref index, "--report");
                    break;
                case "--results-dir":
                    resultsDirectory = RequireValue(args, ref index, "--results-dir");
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        if (string.IsNullOrWhiteSpace(evalSetPath) || string.IsNullOrWhiteSpace(skillPath) || string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("--eval-set, --skill-path, and --model are required");
        }

        var fullSkillPath = Path.GetFullPath(skillPath);
        if (!File.Exists(Path.Combine(fullSkillPath, "SKILL.md")))
        {
            Console.Error.WriteLine($"Error: No SKILL.md found at {fullSkillPath}");
            return 1;
        }

        var parsedSkill = SkillCreatorSupport.ParseSkillMd(fullSkillPath);
        var evalSet = DescriptionOptimizationSupport.LoadEvalSet(Path.GetFullPath(evalSetPath));

        string? liveReportPath = null;
        if (!string.Equals(report, "none", StringComparison.OrdinalIgnoreCase))
        {
            liveReportPath = string.Equals(report, "auto", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(Path.GetTempPath(), $"skill_description_report_{Path.GetFileName(fullSkillPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.html")
                : Path.GetFullPath(report);
            File.WriteAllText(liveReportPath, "<html><body><h1>Starting optimization loop...</h1><meta http-equiv='refresh' content='5'></body></html>");
            SkillCreatorSupport.TryOpenBrowser(liveReportPath);
        }

        string? resultsRoot = null;
        if (!string.IsNullOrWhiteSpace(resultsDirectory))
        {
            resultsRoot = Path.Combine(Path.GetFullPath(resultsDirectory), DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
            Directory.CreateDirectory(resultsRoot);
        }

        var logDirectory = resultsRoot is null ? null : Path.Combine(resultsRoot, "logs");
        var output = await RunLoopAsync(
            evalSet,
            fullSkillPath,
            parsedSkill,
            descriptionOverride,
            numWorkers,
            timeout,
            maxIterations,
            runsPerQuery,
            triggerThreshold,
            holdout,
            model,
            verbose,
            liveReportPath,
            logDirectory);

        var json = JsonSerializer.Serialize(output, SkillCreatorSupport.PrettyJson);
        Console.WriteLine(json);

        if (!string.IsNullOrWhiteSpace(resultsRoot))
        {
            File.WriteAllText(Path.Combine(resultsRoot, "results.json"), json);
        }

        if (!string.IsNullOrWhiteSpace(liveReportPath))
        {
            var finalHtml = ReportSupport.GenerateHtml(output, autoRefresh: false, skillName: parsedSkill.Name);
            File.WriteAllText(liveReportPath, finalHtml);
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Report: {liveReportPath}");

            if (!string.IsNullOrWhiteSpace(resultsRoot))
            {
                File.WriteAllText(Path.Combine(resultsRoot, "report.html"), finalHtml);
            }
        }

        if (!string.IsNullOrWhiteSpace(resultsRoot))
        {
            Console.Error.WriteLine($"Results saved to: {resultsRoot}");
        }

        return 0;
    }

    private static async Task<LoopOutput> RunLoopAsync(
        IReadOnlyList<EvalItem> evalSet,
        string skillPath,
        ParsedSkill parsedSkill,
        string? descriptionOverride,
        int numWorkers,
        int timeout,
        int maxIterations,
        int runsPerQuery,
        double triggerThreshold,
        double holdout,
        string model,
        bool verbose,
        string? liveReportPath,
        string? logDirectory)
    {
        var projectRoot = DescriptionOptimizationSupport.FindProjectRoot();
        var currentDescription = descriptionOverride ?? parsedSkill.Description;
        var history = new List<HistoryEntry>();
        var exitReason = "unknown";

        List<EvalItem> trainSet;
        List<EvalItem> testSet;
        if (holdout > 0)
        {
            (trainSet, testSet) = DescriptionOptimizationSupport.SplitEvalSet(evalSet, holdout);
            if (verbose)
            {
                Console.Error.WriteLine($"Split: {trainSet.Count} train, {testSet.Count} test (holdout={holdout})");
            }
        }
        else
        {
            trainSet = evalSet.ToList();
            testSet = [];
        }

        for (var iteration = 1; iteration <= maxIterations; iteration++)
        {
            if (verbose)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(new string('=', 60));
                Console.Error.WriteLine($"Iteration {iteration}/{maxIterations}");
                Console.Error.WriteLine($"Description: {currentDescription}");
                Console.Error.WriteLine(new string('=', 60));
            }

            var allQueries = trainSet.Concat(testSet).ToList();
            var startedAt = DateTimeOffset.UtcNow;
            var allResults = await DescriptionOptimizationSupport.RunEvalAsync(
                allQueries,
                parsedSkill.Name,
                currentDescription,
                numWorkers,
                timeout,
                projectRoot,
                runsPerQuery,
                triggerThreshold,
                model);
            var elapsed = DateTimeOffset.UtcNow - startedAt;

            var trainQuerySet = trainSet.Select(item => item.Query).ToHashSet(StringComparer.Ordinal);
            var trainResults = allResults.Results.Where(result => trainQuerySet.Contains(result.Query)).ToList();
            var testResultsList = allResults.Results.Where(result => !trainQuerySet.Contains(result.Query)).ToList();

            var trainSummary = new EvalSummary
            {
                Passed = trainResults.Count(result => result.Pass),
                Total = trainResults.Count
            };
            trainSummary.Failed = trainSummary.Total - trainSummary.Passed;

            EvalSummary? testSummary = null;
            if (testSet.Count > 0)
            {
                testSummary = new EvalSummary
                {
                    Passed = testResultsList.Count(result => result.Pass),
                    Total = testResultsList.Count
                };
                testSummary.Failed = testSummary.Total - testSummary.Passed;
            }

            history.Add(new HistoryEntry
            {
                Iteration = iteration,
                Description = currentDescription,
                TrainPassed = trainSummary.Passed,
                TrainFailed = trainSummary.Failed,
                TrainTotal = trainSummary.Total,
                TrainResults = trainResults,
                TestPassed = testSummary?.Passed,
                TestFailed = testSummary?.Failed,
                TestTotal = testSummary?.Total,
                TestResults = testSummary is null ? null : testResultsList,
                Passed = trainSummary.Passed,
                Failed = trainSummary.Failed,
                Total = trainSummary.Total,
                Results = trainResults
            });

            if (!string.IsNullOrWhiteSpace(liveReportPath))
            {
                File.WriteAllText(liveReportPath, ReportSupport.GenerateHtml(new LoopOutput
                {
                    OriginalDescription = parsedSkill.Description,
                    BestDescription = currentDescription,
                    BestScore = "in progress",
                    IterationsRun = history.Count,
                    Holdout = holdout,
                    TrainSize = trainSet.Count,
                    TestSize = testSet.Count,
                    History = history
                }, autoRefresh: true, skillName: parsedSkill.Name));
            }

            if (verbose)
            {
                PrintEvalStats("Train", trainResults, elapsed);
                if (testSummary is not null)
                {
                    PrintEvalStats("Test ", testResultsList, TimeSpan.Zero);
                }
            }

            if (trainSummary.Failed == 0)
            {
                exitReason = $"all_passed (iteration {iteration})";
                if (verbose)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine($"All train queries passed on iteration {iteration}!");
                }
                break;
            }

            if (iteration == maxIterations)
            {
                exitReason = $"max_iterations ({maxIterations})";
                if (verbose)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine($"Max iterations reached ({maxIterations}).");
                }
                break;
            }

            if (verbose)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Improving description...");
            }

            var blindedHistory = history.Select(entry => new HistoryEntry
            {
                Iteration = entry.Iteration,
                Description = entry.Description,
                TrainPassed = entry.TrainPassed,
                TrainFailed = entry.TrainFailed,
                TrainTotal = entry.TrainTotal,
                Passed = entry.Passed,
                Failed = entry.Failed,
                Total = entry.Total,
                Results = entry.Results,
                Note = entry.Note
            }).ToList();

            var improveStartedAt = DateTimeOffset.UtcNow;
            currentDescription = await DescriptionOptimizationSupport.ImproveDescriptionAsync(
                parsedSkill.Name,
                parsedSkill.Content,
                currentDescription,
                new EvalResultsDocument
                {
                    SkillName = parsedSkill.Name,
                    Description = currentDescription,
                    Results = trainResults,
                    Summary = trainSummary
                },
                blindedHistory,
                model,
                logDirectory: logDirectory,
                iteration: iteration);
            var improveElapsed = DateTimeOffset.UtcNow - improveStartedAt;

            if (verbose)
            {
                Console.Error.WriteLine($"Proposed ({improveElapsed.TotalSeconds:F1}s): {currentDescription}");
            }
        }

        var best = testSet.Count > 0
            ? history.OrderByDescending(entry => entry.TestPassed ?? 0).First()
            : history.OrderByDescending(entry => entry.TrainPassed ?? entry.Passed ?? 0).First();

        var bestScore = testSet.Count > 0
            ? $"{best.TestPassed}/{best.TestTotal}"
            : $"{best.TrainPassed}/{best.TrainTotal}";

        if (verbose)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Exit reason: {exitReason}");
            Console.Error.WriteLine($"Best score: {bestScore} (iteration {best.Iteration})");
        }

        return new LoopOutput
        {
            ExitReason = exitReason,
            OriginalDescription = parsedSkill.Description,
            BestDescription = best.Description,
            BestScore = bestScore,
            BestTrainScore = $"{best.TrainPassed}/{best.TrainTotal}",
            BestTestScore = testSet.Count > 0 ? $"{best.TestPassed}/{best.TestTotal}" : null,
            FinalDescription = currentDescription,
            IterationsRun = history.Count,
            Holdout = holdout,
            TrainSize = trainSet.Count,
            TestSize = testSet.Count,
            History = history
        };
    }

    private static void PrintEvalStats(string label, IReadOnlyList<EvalResult> results, TimeSpan elapsed)
    {
        var positive = results.Where(result => result.ShouldTrigger).ToList();
        var negative = results.Where(result => !result.ShouldTrigger).ToList();

        var truePositive = positive.Sum(result => result.Triggers);
        var positiveRuns = positive.Sum(result => result.Runs);
        var falseNegative = positiveRuns - truePositive;
        var falsePositive = negative.Sum(result => result.Triggers);
        var negativeRuns = negative.Sum(result => result.Runs);
        var trueNegative = negativeRuns - falsePositive;
        var total = truePositive + trueNegative + falsePositive + falseNegative;

        var precision = truePositive + falsePositive > 0 ? truePositive / (double)(truePositive + falsePositive) : 1.0;
        var recall = truePositive + falseNegative > 0 ? truePositive / (double)(truePositive + falseNegative) : 1.0;
        var accuracy = total > 0 ? (truePositive + trueNegative) / (double)total : 0.0;

        Console.Error.WriteLine($"{label}: {truePositive + trueNegative}/{total} correct, precision={precision:0%} recall={recall:0%} accuracy={accuracy:0%} ({elapsed.TotalSeconds:F1}s)");
        foreach (var result in results)
        {
            var status = result.Pass ? "PASS" : "FAIL";
            Console.Error.WriteLine($"  [{status}] rate={result.Triggers}/{result.Runs} expected={result.ShouldTrigger}: {result.Query[..Math.Min(result.Query.Length, 60)]}");
        }
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

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --file scripts\\run_loop.cs -- --eval-set <path> --skill-path <path> --model <model> [--description <text>] [--num-workers <n>] [--timeout <seconds>] [--max-iterations <n>] [--runs-per-query <n>] [--trigger-threshold <ratio>] [--holdout <ratio>] [--verbose] [--report <path|auto|none>] [--results-dir <path>]");
    }
}
