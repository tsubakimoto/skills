#:property PublishAot=false
#:package YamlDotNet@16.3.0
#:include SkillValidationSupport.cs

if (args.Length != 1 || IsHelpRequest(args[0]))
{
    Console.WriteLine("Usage: dotnet run --file scripts\\quick_validate.cs -- <skill_directory>");
    return args.Length == 1 ? 0 : 1;
}

var (isValid, message) = SkillValidationSupport.ValidateSkill(args[0]);
Console.WriteLine(message);
return isValid ? 0 : 1;

static bool IsHelpRequest(string value) =>
    value is "-h" or "--help" or "/?";
