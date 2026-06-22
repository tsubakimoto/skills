using System.Net;
using System.Text;

internal static class ReportSupport
{
    public static string GenerateHtml(LoopOutput data, bool autoRefresh = false, string skillName = "")
    {
        var history = data.History ?? [];
        var titlePrefix = string.IsNullOrWhiteSpace(skillName) ? string.Empty : $"{WebUtility.HtmlEncode(skillName)} — ";
        var trainQueries = history.Count > 0
            ? (history[0].TrainResults ?? history[0].Results ?? []).Select(result => (result.Query, result.ShouldTrigger)).ToList()
            : [];
        var testQueries = history.Count > 0
            ? (history[0].TestResults ?? []).Select(result => (result.Query, result.ShouldTrigger)).ToList()
            : [];

        var refreshTag = autoRefresh ? "    <meta http-equiv=\"refresh\" content=\"5\">\n" : string.Empty;
        var builder = new StringBuilder();
        builder.Append("""
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
""");
        builder.Append(refreshTag);
        builder.AppendLine($"    <title>{titlePrefix}Skill Description Optimization</title>");
        builder.Append("""
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Poppins:wght@500;600&family=Lora:wght@400;500&display=swap" rel="stylesheet">
    <style>
        body { font-family: 'Lora', Georgia, serif; max-width: 100%; margin: 0 auto; padding: 20px; background: #faf9f5; color: #141413; }
        h1 { font-family: 'Poppins', sans-serif; color: #141413; }
        .explainer, .summary { background: white; padding: 15px; border-radius: 6px; margin-bottom: 20px; border: 1px solid #e8e6dc; }
        .explainer { color: #b0aea5; font-size: 0.875rem; line-height: 1.6; }
        .summary p { margin: 5px 0; }
        .best { color: #788c5d; font-weight: bold; }
        .table-container { overflow-x: auto; width: 100%; }
        table { border-collapse: collapse; background: white; border: 1px solid #e8e6dc; border-radius: 6px; font-size: 12px; min-width: 100%; }
        th, td { padding: 8px; text-align: left; border: 1px solid #e8e6dc; white-space: normal; word-wrap: break-word; }
        th { font-family: 'Poppins', sans-serif; background: #141413; color: #faf9f5; font-weight: 500; }
        th.test-col { background: #6a9bcc; }
        th.query-col { min-width: 200px; }
        td.description { font-family: monospace; font-size: 11px; word-wrap: break-word; max-width: 400px; }
        td.result { text-align: center; font-size: 16px; min-width: 40px; }
        td.test-result { background: #f0f6fc; }
        .pass { color: #788c5d; }
        .fail { color: #c44; }
        .rate { font-size: 9px; color: #b0aea5; display: block; }
        tr:hover { background: #faf9f5; }
        .score { display: inline-block; padding: 2px 6px; border-radius: 4px; font-weight: bold; font-size: 11px; }
        .score-good { background: #eef2e8; color: #788c5d; }
        .score-ok { background: #fef3c7; color: #d97706; }
        .score-bad { background: #fceaea; color: #c44; }
        .best-row { background: #f5f8f2; }
        th.positive-col { border-bottom: 3px solid #788c5d; }
        th.negative-col { border-bottom: 3px solid #c44; }
        th.test-col.positive-col { border-bottom: 3px solid #788c5d; }
        th.test-col.negative-col { border-bottom: 3px solid #c44; }
        .legend { font-family: 'Poppins', sans-serif; display: flex; gap: 20px; margin-bottom: 10px; font-size: 13px; align-items: center; }
        .legend-item { display: flex; align-items: center; gap: 6px; }
        .legend-swatch { width: 16px; height: 16px; border-radius: 3px; display: inline-block; }
        .swatch-positive { background: #141413; border-bottom: 3px solid #788c5d; }
        .swatch-negative { background: #141413; border-bottom: 3px solid #c44; }
        .swatch-test { background: #6a9bcc; }
        .swatch-train { background: #141413; }
    </style>
</head>
<body>
""");
        builder.AppendLine($"    <h1>{titlePrefix}Skill Description Optimization</h1>");
        builder.Append("""
    <div class="explainer">
        <strong>Optimizing your skill's description.</strong> This page updates automatically as Claude tests different versions of your skill's description. Each row is an iteration — a new description attempt. The columns show test queries: green checkmarks mean the skill triggered correctly (or correctly didn't trigger), red crosses mean it got it wrong. The "Train" score shows performance on queries used to improve the description; the "Test" score shows performance on held-out queries the optimizer hasn't seen. When it's done, Claude will apply the best-performing description to your skill.
    </div>
""");
        builder.AppendLine($"""
    <div class="summary">
        <p><strong>Original:</strong> {WebUtility.HtmlEncode(data.OriginalDescription)}</p>
        <p class="best"><strong>Best:</strong> {WebUtility.HtmlEncode(data.BestDescription)}</p>
        <p><strong>Best Score:</strong> {WebUtility.HtmlEncode(data.BestScore)} {(string.IsNullOrWhiteSpace(data.BestTestScore) ? "(train)" : "(test)")}</p>
        <p><strong>Iterations:</strong> {data.IterationsRun} | <strong>Train:</strong> {data.TrainSize} | <strong>Test:</strong> {data.TestSize}</p>
    </div>
""");
        builder.Append("""
    <div class="legend">
        <span style="font-weight:600">Query columns:</span>
        <span class="legend-item"><span class="legend-swatch swatch-positive"></span> Should trigger</span>
        <span class="legend-item"><span class="legend-swatch swatch-negative"></span> Should NOT trigger</span>
        <span class="legend-item"><span class="legend-swatch swatch-train"></span> Train</span>
        <span class="legend-item"><span class="legend-swatch swatch-test"></span> Test</span>
    </div>
    <div class="table-container">
    <table>
        <thead>
            <tr>
                <th>Iter</th>
                <th>Train</th>
                <th>Test</th>
                <th class="query-col">Description</th>
""");

        foreach (var (query, shouldTrigger) in trainQueries)
        {
            var polarity = shouldTrigger ? "positive-col" : "negative-col";
            builder.AppendLine($"                <th class=\"{polarity}\">{WebUtility.HtmlEncode(query)}</th>");
        }

        foreach (var (query, shouldTrigger) in testQueries)
        {
            var polarity = shouldTrigger ? "positive-col" : "negative-col";
            builder.AppendLine($"                <th class=\"test-col {polarity}\">{WebUtility.HtmlEncode(query)}</th>");
        }

        builder.Append("""
            </tr>
        </thead>
        <tbody>
""");

        var bestIteration = testQueries.Count > 0
            ? history.OrderByDescending(entry => entry.TestPassed ?? 0).FirstOrDefault()?.Iteration
            : history.OrderByDescending(entry => entry.TrainPassed ?? entry.Passed ?? 0).FirstOrDefault()?.Iteration;

        foreach (var entry in history)
        {
            var trainResults = entry.TrainResults ?? entry.Results ?? [];
            var testResults = entry.TestResults ?? [];
            var trainLookup = trainResults.ToDictionary(result => result.Query, result => result, StringComparer.Ordinal);
            var testLookup = testResults.ToDictionary(result => result.Query, result => result, StringComparer.Ordinal);

            var (trainCorrect, trainRuns) = AggregateRuns(trainResults);
            var (testCorrect, testRuns) = AggregateRuns(testResults);

            var rowClass = entry.Iteration == bestIteration ? "best-row" : string.Empty;
            builder.AppendLine($"""
            <tr class="{rowClass}">
                <td>{entry.Iteration?.ToString() ?? "?"}</td>
                <td><span class="score {GetScoreClass(trainCorrect, trainRuns)}">{trainCorrect}/{trainRuns}</span></td>
                <td><span class="score {GetScoreClass(testCorrect, testRuns)}">{testCorrect}/{testRuns}</span></td>
                <td class="description">{WebUtility.HtmlEncode(entry.Description)}</td>
""");

            foreach (var (query, _) in trainQueries)
            {
                AppendResultCell(builder, trainLookup.TryGetValue(query, out var result) ? result : null, false);
            }

            foreach (var (query, _) in testQueries)
            {
                AppendResultCell(builder, testLookup.TryGetValue(query, out var result) ? result : null, true);
            }

            builder.AppendLine("            </tr>");
        }

        builder.Append("""
        </tbody>
    </table>
    </div>
</body>
</html>
""");

        return builder.ToString();
    }

    private static (int Correct, int Total) AggregateRuns(IEnumerable<EvalResult> results)
    {
        var correct = 0;
        var total = 0;
        foreach (var result in results)
        {
            total += result.Runs;
            correct += result.ShouldTrigger ? result.Triggers : result.Runs - result.Triggers;
        }

        return (correct, total);
    }

    private static string GetScoreClass(int correct, int total)
    {
        if (total <= 0)
        {
            return "score-bad";
        }

        var ratio = correct / (double)total;
        return ratio switch
        {
            >= 0.8 => "score-good",
            >= 0.5 => "score-ok",
            _ => "score-bad"
        };
    }

    private static void AppendResultCell(StringBuilder builder, EvalResult? result, bool isTest)
    {
        var passed = result?.Pass ?? false;
        var icon = passed ? "✓" : "✗";
        var cssClass = passed ? "pass" : "fail";
        var extraClass = isTest ? " test-result" : string.Empty;
        var triggers = result?.Triggers ?? 0;
        var runs = result?.Runs ?? 0;
        builder.AppendLine($"                <td class=\"result{extraClass} {cssClass}\">{icon}<span class=\"rate\">{triggers}/{runs}</span></td>");
    }
}
