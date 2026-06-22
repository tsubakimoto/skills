using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

internal sealed record ParsedSkill(string Name, string Description, string Content);

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

internal static class SkillCreatorSupport
{
    public static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true
    };

    public static ParsedSkill ParseSkillMd(string skillDirectory)
    {
        var skillPath = Path.GetFullPath(skillDirectory);
        var skillMdPath = Path.Combine(skillPath, "SKILL.md");
        var content = File.ReadAllText(skillMdPath);
        var normalized = NormalizeNewlines(content);
        var lines = normalized.Split('\n');

        if (lines.Length == 0 || lines[0].Trim() != "---")
        {
            throw new InvalidOperationException("SKILL.md missing frontmatter (no opening ---)");
        }

        var endIndex = Array.FindIndex(lines, 1, line => line.Trim() == "---");
        if (endIndex < 0)
        {
            throw new InvalidOperationException("SKILL.md missing frontmatter (no closing ---)");
        }

        var frontmatterLines = lines[1..endIndex];
        var name = string.Empty;
        var description = string.Empty;

        for (var index = 0; index < frontmatterLines.Length; index++)
        {
            var line = frontmatterLines[index];
            if (line.StartsWith("name:", StringComparison.Ordinal))
            {
                name = Unquote(line["name:".Length..].Trim());
                continue;
            }

            if (!line.StartsWith("description:", StringComparison.Ordinal))
            {
                continue;
            }

            var value = line["description:".Length..].Trim();
            if (value is ">" or "|" or ">-" or "|-")
            {
                var continuationLines = new List<string>();
                index++;
                while (index < frontmatterLines.Length &&
                    (frontmatterLines[index].StartsWith("  ", StringComparison.Ordinal) ||
                     frontmatterLines[index].StartsWith('\t')))
                {
                    continuationLines.Add(frontmatterLines[index].Trim());
                    index++;
                }

                index--;
                description = string.Join(' ', continuationLines);
                continue;
            }

            description = Unquote(value);
        }

        return new ParsedSkill(name, description, content);
    }

    public static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IEnumerable<string> arguments,
        string? standardInput = null,
        string? workingDirectory = null,
        int timeoutSeconds = 300,
        bool removeClaudeCodeEnvironment = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (removeClaudeCodeEnvironment)
        {
            startInfo.Environment.Remove("CLAUDECODE");
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = await outputTask;
            var stderr = await errorTask;
            return new ProcessResult(process.ExitCode, stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw new TimeoutException($"Process '{fileName}' timed out after {timeoutSeconds} seconds.");
        }
    }

    public static void TryOpenBrowser(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    public static string NormalizeNewlines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    public static string GetSourceDirectory([CallerFilePath] string filePath = "") =>
        Path.GetDirectoryName(filePath)
        ?? throw new InvalidOperationException("Source directory is unavailable.");

    private static string Unquote(string value) =>
        value.Trim().Trim('"').Trim('\'');

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
        catch
        {
        }
    }
}
