#:property PublishAot=false
#:package YamlDotNet@16.3.0
#:include SkillValidationSupport.cs

using System.IO.Compression;

return PackageSkillCli.Run(args);

static class PackageSkillCli
{
    private static readonly HashSet<string> ExcludeDirectories = ["__pycache__", "node_modules"];
    private static readonly string[] ExcludeGlobs = ["*.pyc"];
    private static readonly HashSet<string> ExcludeFiles = [".DS_Store"];
    private static readonly HashSet<string> RootExcludeDirectories = ["evals"];

    public static int Run(string[] args)
    {
        if (args.Length is < 1 or > 2 || args.Any(IsHelpToken))
        {
            PrintUsage();
            return args.Any(IsHelpToken) ? 0 : 1;
        }

        var skillPath = args[0];
        var outputDirectory = args.Length > 1 ? args[1] : null;

        Console.WriteLine($"📦 Packaging skill: {skillPath}");
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Console.WriteLine($"   Output directory: {outputDirectory}");
        }

        Console.WriteLine();
        return PackageSkill(skillPath, outputDirectory) is null ? 1 : 0;
    }

    private static string? PackageSkill(string skillPath, string? outputDirectory)
    {
        var fullSkillPath = Path.GetFullPath(skillPath);
        if (!Directory.Exists(fullSkillPath))
        {
            Console.WriteLine($"❌ Error: Skill folder not found: {fullSkillPath}");
            return null;
        }

        var skillMdPath = Path.Combine(fullSkillPath, "SKILL.md");
        if (!File.Exists(skillMdPath))
        {
            Console.WriteLine($"❌ Error: SKILL.md not found in {fullSkillPath}");
            return null;
        }

        Console.WriteLine("🔍 Validating skill...");
        var (isValid, message) = SkillValidationSupport.ValidateSkill(fullSkillPath);
        if (!isValid)
        {
            Console.WriteLine($"❌ Validation failed: {message}");
            Console.WriteLine("   Please fix the validation errors before packaging.");
            return null;
        }

        Console.WriteLine($"✅ {message}");
        Console.WriteLine();

        var skillDirectory = new DirectoryInfo(fullSkillPath);
        var outputPath = string.IsNullOrWhiteSpace(outputDirectory)
            ? Environment.CurrentDirectory
            : Directory.CreateDirectory(Path.GetFullPath(outputDirectory)).FullName;
        var packagePath = Path.Combine(outputPath, $"{skillDirectory.Name}.skill");

        try
        {
            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }

            using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
            foreach (var file in skillDirectory.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                var archiveName = Path.GetRelativePath(skillDirectory.Parent?.FullName ?? fullSkillPath, file.FullName);
                if (ShouldExclude(archiveName))
                {
                    Console.WriteLine($"  Skipped: {archiveName}");
                    continue;
                }

                archive.CreateEntryFromFile(file.FullName, archiveName, CompressionLevel.SmallestSize);
                Console.WriteLine($"  Added: {archiveName}");
            }

            Console.WriteLine();
            Console.WriteLine($"✅ Successfully packaged skill to: {packagePath}");
            return packagePath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error creating .skill file: {ex.Message}");
            return null;
        }
    }

    private static bool ShouldExclude(string relativePath)
    {
        var parts = relativePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(ExcludeDirectories.Contains))
        {
            return true;
        }

        if (parts.Length > 1 && RootExcludeDirectories.Contains(parts[1]))
        {
            return true;
        }

        var name = Path.GetFileName(relativePath);
        if (ExcludeFiles.Contains(name))
        {
            return true;
        }

        return ExcludeGlobs.Any(pattern => MatchesGlob(name, pattern));
    }

    private static bool MatchesGlob(string fileName, string pattern)
    {
        if (pattern == "*.pyc")
        {
            return fileName.EndsWith(".pyc", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsHelpToken(string value) =>
        value is "-h" or "--help" or "/?";

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --file scripts\\package_skill.cs -- <path\\to\\skill-folder> [output-directory]");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  dotnet run --file scripts\\package_skill.cs -- skills\\public\\my-skill");
        Console.WriteLine("  dotnet run --file scripts\\package_skill.cs -- skills\\public\\my-skill .\\dist");
    }
}
