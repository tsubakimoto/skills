using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

internal static class SkillValidationSupport
{
    private static readonly Regex FrontmatterRegex =
        new(@"^---\r?\n(.*?)\r?\n---", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly IDeserializer Deserializer =
        new DeserializerBuilder().Build();

    private static readonly HashSet<string> AllowedProperties =
    [
        "name",
        "description",
        "license",
        "allowed-tools",
        "metadata",
        "compatibility"
    ];

    public static (bool IsValid, string Message) ValidateSkill(string skillPath)
    {
        var fullPath = Path.GetFullPath(skillPath);
        var skillMdPath = Path.Combine(fullPath, "SKILL.md");
        if (!File.Exists(skillMdPath))
        {
            return (false, "SKILL.md not found");
        }

        var content = File.ReadAllText(skillMdPath);
        if (!content.StartsWith("---", StringComparison.Ordinal))
        {
            return (false, "No YAML frontmatter found");
        }

        var match = FrontmatterRegex.Match(content);
        if (!match.Success)
        {
            return (false, "Invalid frontmatter format");
        }

        Dictionary<object, object?> frontmatter;
        try
        {
            frontmatter = Deserializer.Deserialize<Dictionary<object, object?>>(match.Groups[1].Value)
                ?? throw new YamlException("Frontmatter was empty.");
        }
        catch (YamlException ex)
        {
            return (false, $"Invalid YAML in frontmatter: {ex.Message}");
        }

        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in frontmatter)
        {
            if (pair.Key is not string key)
            {
                return (false, "Frontmatter keys must be strings");
            }

            normalized[key] = pair.Value;
        }

        var unexpectedKeys = normalized.Keys.Where(key => !AllowedProperties.Contains(key)).OrderBy(key => key).ToArray();
        if (unexpectedKeys.Length > 0)
        {
            return (false,
                $"Unexpected key(s) in SKILL.md frontmatter: {string.Join(", ", unexpectedKeys)}. Allowed properties are: {string.Join(", ", AllowedProperties.OrderBy(key => key))}");
        }

        if (!normalized.TryGetValue("name", out var rawName))
        {
            return (false, "Missing 'name' in frontmatter");
        }

        if (rawName is not string nameText)
        {
            return (false, $"Name must be a string, got {DescribeType(rawName)}");
        }

        var name = nameText.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            if (!Regex.IsMatch(name, "^[a-z0-9-]+$"))
            {
                return (false, $"Name '{name}' should be kebab-case (lowercase letters, digits, and hyphens only)");
            }

            if (name.StartsWith('-', StringComparison.Ordinal) ||
                name.EndsWith('-', StringComparison.Ordinal) ||
                name.Contains("--", StringComparison.Ordinal))
            {
                return (false, $"Name '{name}' cannot start/end with hyphen or contain consecutive hyphens");
            }

            if (name.Length > 64)
            {
                return (false, $"Name is too long ({name.Length} characters). Maximum is 64 characters.");
            }
        }

        if (!normalized.TryGetValue("description", out var rawDescription))
        {
            return (false, "Missing 'description' in frontmatter");
        }

        if (rawDescription is not string descriptionText)
        {
            return (false, $"Description must be a string, got {DescribeType(rawDescription)}");
        }

        var description = descriptionText.Trim();
        if (!string.IsNullOrEmpty(description))
        {
            if (description.Contains('<', StringComparison.Ordinal) || description.Contains('>', StringComparison.Ordinal))
            {
                return (false, "Description cannot contain angle brackets (< or >)");
            }

            if (description.Length > 1024)
            {
                return (false, $"Description is too long ({description.Length} characters). Maximum is 1024 characters.");
            }
        }

        if (normalized.TryGetValue("compatibility", out var rawCompatibility) && rawCompatibility is not null)
        {
            if (rawCompatibility is not string compatibilityText)
            {
                return (false, $"Compatibility must be a string, got {DescribeType(rawCompatibility)}");
            }

            if (compatibilityText.Length > 500)
            {
                return (false, $"Compatibility is too long ({compatibilityText.Length} characters). Maximum is 500 characters.");
            }
        }

        return (true, "Skill is valid!");
    }

    private static string DescribeType(object? value) =>
        value?.GetType().Name ?? "null";
}
