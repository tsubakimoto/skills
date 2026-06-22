#:property PublishAot=false
#:include SkillCreatorSupport.cs
#:include DescriptionOptimizationSupport.cs

using System.Text.Json;

return await ImproveDescriptionCli.RunAsync(args);

static class ImproveDescriptionCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args.Any(IsHelpToken))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        string? evalResultsPath = null;
        string? skillPath = null;
        string? historyPath = null;
        string? model = null;
        var verbose = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--eval-results":
                    evalResultsPath = RequireValue(args, ref index, "--eval-results");
                    break;
                case "--skill-path":
                    skillPath = RequireValue(args, ref index, "--skill-path");
                    break;
                case "--history":
                    historyPath = RequireValue(args, ref index, "--history");
                    break;
                case "--model":
                    model = RequireValue(args, ref index, "--model");
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        if (string.IsNullOrWhiteSpace(evalResultsPath) || string.IsNullOrWhiteSpace(skillPath) || string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("--eval-results, --skill-path, and --model are required");
        }

        var fullSkillPath = Path.GetFullPath(skillPath);
        if (!File.Exists(Path.Combine(fullSkillPath, "SKILL.md")))
        {
            Console.Error.WriteLine($"Error: No SKILL.md found at {fullSkillPath}");
            return 1;
        }

        var evalResults = JsonSerializer.Deserialize<EvalResultsDocument>(File.ReadAllText(Path.GetFullPath(evalResultsPath))) ?? new EvalResultsDocument();
        var history = !string.IsNullOrWhiteSpace(historyPath)
            ? DescriptionOptimizationSupport.LoadHistory(Path.GetFullPath(historyPath))
            : [];
        var parsedSkill = SkillCreatorSupport.ParseSkillMd(fullSkillPath);
        var currentDescription = evalResults.Description;

        if (verbose)
        {
            Console.Error.WriteLine($"Current: {currentDescription}");
            Console.Error.WriteLine($"Score: {evalResults.Summary.Passed}/{evalResults.Summary.Total}");
        }

        var newDescription = await DescriptionOptimizationSupport.ImproveDescriptionAsync(
            parsedSkill.Name,
            parsedSkill.Content,
            currentDescription,
            evalResults,
            history,
            model);

        if (verbose)
        {
            Console.Error.WriteLine($"Improved: {newDescription}");
        }

        var output = new
        {
            description = newDescription,
            history = history.Concat(new[]
            {
                new HistoryEntry
                {
                    Description = currentDescription,
                    Passed = evalResults.Summary.Passed,
                    Failed = evalResults.Summary.Failed,
                    Total = evalResults.Summary.Total,
                    Results = evalResults.Results
                }
            }).ToList()
        };

        Console.WriteLine(JsonSerializer.Serialize(output, SkillCreatorSupport.PrettyJson));
        return 0;
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
        Console.WriteLine("Usage: dotnet run --file scripts\\improve_description.cs -- --eval-results <path> --skill-path <path> --model <model> [--history <path>] [--verbose]");
    }
}
