#:property PublishAot=false
#:include SkillCreatorSupport.cs
#:include DescriptionOptimizationSupport.cs

using System.Text.Json;

return await RunEvalCli.RunAsync(args);

static class RunEvalCli
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
        var runsPerQuery = 3;
        var triggerThreshold = 0.5;
        string? model = null;
        var verbose = false;

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
                case "--runs-per-query":
                    runsPerQuery = int.Parse(RequireValue(args, ref index, "--runs-per-query"));
                    break;
                case "--trigger-threshold":
                    triggerThreshold = double.Parse(RequireValue(args, ref index, "--trigger-threshold"));
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

        if (string.IsNullOrWhiteSpace(evalSetPath) || string.IsNullOrWhiteSpace(skillPath))
        {
            throw new ArgumentException("--eval-set and --skill-path are required");
        }

        var fullSkillPath = Path.GetFullPath(skillPath);
        if (!File.Exists(Path.Combine(fullSkillPath, "SKILL.md")))
        {
            Console.Error.WriteLine($"Error: No SKILL.md found at {fullSkillPath}");
            return 1;
        }

        var parsedSkill = SkillCreatorSupport.ParseSkillMd(fullSkillPath);
        var description = descriptionOverride ?? parsedSkill.Description;
        var projectRoot = DescriptionOptimizationSupport.FindProjectRoot();
        var evalSet = DescriptionOptimizationSupport.LoadEvalSet(Path.GetFullPath(evalSetPath));

        if (verbose)
        {
            Console.Error.WriteLine($"Evaluating: {description}");
        }

        var output = await DescriptionOptimizationSupport.RunEvalAsync(
            evalSet,
            parsedSkill.Name,
            description,
            numWorkers,
            timeout,
            projectRoot,
            runsPerQuery,
            triggerThreshold,
            model);

        if (verbose)
        {
            Console.Error.WriteLine($"Results: {output.Summary.Passed}/{output.Summary.Total} passed");
            foreach (var result in output.Results)
            {
                var status = result.Pass ? "PASS" : "FAIL";
                Console.Error.WriteLine($"  [{status}] rate={result.Triggers}/{result.Runs} expected={result.ShouldTrigger}: {result.Query[..Math.Min(result.Query.Length, 70)]}");
            }
        }

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
        Console.WriteLine("Usage: dotnet run --file scripts\\run_eval.cs -- --eval-set <path> --skill-path <path> [--description <text>] [--num-workers <n>] [--timeout <seconds>] [--runs-per-query <n>] [--trigger-threshold <ratio>] [--model <model>] [--verbose]");
    }
}
