#:property PublishAot=false
#:include SkillCreatorSupport.cs
#:include DescriptionOptimizationSupport.cs
#:include ReportSupport.cs

using System.Text.Json;

return await GenerateReportCli.RunAsync(args);

static class GenerateReportCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args.Any(IsHelpToken))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var input = args[0];
        string? output = null;
        var skillName = string.Empty;

        for (var index = 1; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--output":
                case "-o":
                    output = RequireValue(args, ref index, args[index]);
                    break;
                case "--skill-name":
                    skillName = RequireValue(args, ref index, "--skill-name");
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        var json = input == "-"
            ? await Console.In.ReadToEndAsync()
            : File.ReadAllText(Path.GetFullPath(input));
        var data = JsonSerializer.Deserialize<LoopOutput>(json) ?? new LoopOutput();
        var html = ReportSupport.GenerateHtml(data, skillName: skillName);

        if (!string.IsNullOrWhiteSpace(output))
        {
            var fullOutput = Path.GetFullPath(output);
            var directory = Path.GetDirectoryName(fullOutput);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullOutput, html);
            Console.Error.WriteLine($"Report written to {fullOutput}");
            return 0;
        }

        Console.Write(html);
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
        Console.WriteLine("Usage: dotnet run --file scripts\\generate_report.cs -- <input|-> [-o <output>] [--skill-name <name>]");
    }
}
