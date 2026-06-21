---
name: csharp-file-based-apps
description: >
  Use this skill whenever the user is working with C# file-based apps on the latest .NET,
  including single-file C# programs run with `dotnet run file.cs`, `dotnet file.cs`, or stdin,
  `#:` directives such as `#:package`, `#:property`, `#:project`, `#:sdk`, and `#:include`,
  converting utility scripts into file-based apps, packaging them as .NET tools, publishing with
  Native AOT, or troubleshooting cache, launch profile, and folder layout issues. Trigger even
  when the user only says "single-file C# app", "projectless C# script", or asks how to run a
  .cs file directly without creating a .csproj.
license: Proprietary. LICENSE has complete terms.
---

# C# File-based Apps Skill

This skill covers **C# file-based apps** on the latest .NET. Use it for single-file `.cs` execution with `dotnet run app.cs` or `dotnet app.cs`, `#:` directives, publish / pack / convert workflows, launch profiles, user secrets, build caching, and folder layout guidance.

## Skill directory

`~/.copilot/skills/csharp-file-based-apps/`

## Quick Reference

- Use `references/file-based-apps.md` for the core reference.
- Start from the minimal samples in the `Examples` section when you need working code.

---

## Workflow

1. First determine whether the user needs **a new file-based app**, **an improvement to an existing utility**, **a conversion from or to a traditional `.csproj`**, or **troubleshooting**.
2. Check `references/file-based-apps.md` for supported directives, CLI behavior, and constraints. If you need examples, prefer the samples in this SKILL.md and the bundled reference.
3. When proposing code or commands, make these points explicit:
   - SDK requirements and prerequisites (**.NET 10 SDK or later**; `#:include` requires SDK 10.0.300+ / .NET 11 Preview 3+)
   - Which `#:` directives are appropriate
   - Whether `dotnet run`, `build`, `publish`, `pack`, `restore`, or `project convert` is the right command
   - Whether implicit files such as `Directory.Build.props` or `global.json` might affect the result
4. Prefer the **smallest single-file example that actually works**, and only introduce extra files or conversion steps when there is a clear reason.
5. Only state behavior as fact when it is supported by Microsoft Learn. Call out preview-only features and OS-specific differences.

---

## What to help with

- Running, building, and distributing a single `.cs` file
- Choosing the right `#:` directives
- NuGet package references, project references, and MSBuild properties
- Native AOT defaults and how to disable them
- Packing as a .NET tool
- Converting to a traditional project with `dotnet project convert`
- Launch profiles via `app.run.json`
- `dotnet user-secrets ... --file app.cs`
- Troubleshooting build cache and folder layout issues

---

## Supported directives

File-based apps place `#:` directives at the top of the C# file.

| Directive | Purpose | Example |
|-----------|------|----|
| `#:package` | Add a NuGet package reference | `#:package Spectre.Console@*` |
| `#:property` | Set an MSBuild property | `#:property PublishAot=false` |
| `#:project` | Reference another project | `#:project ../Shared/Shared.csproj` |
| `#:sdk` | Select the SDK | `#:sdk Microsoft.NET.Sdk.Web` |
| `#:include` | Include extra files | `#:include shared/**/*.cs` |

### Guidance

- Do not invent unsupported directives. Use only the five directives listed above.
- For `#:package`, prefer explicit versions unless the repo uses central package management. Use `@*` when "latest available" is the goal.
- Included `.cs` files in `#:include` cannot contain **top-level statements**. Use them for types, methods, namespaces, and related declarations.
- `#:property` can use MSBuild property functions and environment variables, but keep the setup understandable and explain why it is needed.
- For ASP.NET Core or configuration-driven examples, consider `#:sdk Microsoft.NET.Sdk.Web`.

---

## CLI patterns

### Run

```bash
dotnet run app.cs
dotnet app.cs
dotnet run app.cs -- arg1 arg2
```

- If a `.csproj` exists in the current directory, `dotnet run app.cs` might, for backward compatibility, **run that project and pass `app.cs` as an argument**. Use `--file` when you need unambiguous file-based behavior.

### Build / Clean / Restore

```bash
dotnet build app.cs
dotnet clean app.cs
dotnet restore app.cs
dotnet clean file-based-apps
```

### Publish / Pack / Convert

```bash
dotnet publish app.cs
dotnet pack app.cs
dotnet project convert app.cs
```

- `publish` enables Native AOT by default.
- `pack` defaults to `PackAsTool=true`.
- Use `#:property PublishAot=false` or `#:property PackAsTool=false` when you need to override those defaults.

---

## Troubleshooting checklist

### 1. A project runs when you meant to run the file-based app

- Use `dotnet run --file app.cs`.
- Consider moving `app.cs` outside the `.csproj` directory tree.

### 2. Build caching looks wrong

- `dotnet clean app.cs`
- `dotnet clean file-based-apps`
- If needed, run `dotnet build app.cs` and then `dotnet run app.cs --no-build`

### 3. Implicit build files are affecting behavior

- `Directory.Build.props`
- `Directory.Build.targets`
- `Directory.Packages.props`
- `nuget.config`
- `global.json`

Any of these in parent directories can affect the file-based app and often explain surprising behavior.

### 4. The folder layout is working against you

- Avoid placing a utility-style `app.cs` inside a `.csproj` cone.
- Use a separate directory for standalone scripts and utilities.

---

## Output expectations

When responding, prefer this order when it fits the request:

1. **Shortest correct answer**: what to run, add, or change
2. **Sample code**: the smallest single `.cs` file that works
3. **Notes**: SDK requirements, preview limitations, cache behavior, or folder layout caveats

If the request involves conversion or architectural choice, explain why a file-based app is a good fit and when a normal `.csproj` would be a better option.

---

## Examples

### Minimal console example

```csharp
#:package Spectre.Console@*

using Spectre.Console;

AnsiConsole.MarkupLine("[green]Hello from a file-based app[/]");
```

Run:

```bash
dotnet run hello.cs
```

### Disable Native AOT for a quick utility

```csharp
#:property PublishAot=false

Console.WriteLine("Utility script");
```

### Web app style

```csharp
#:sdk Microsoft.NET.Sdk.Web

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { message = "hello" }));

app.Run();
```

---

## References

- Detailed reference: [references/file-based-apps.md](./references/file-based-apps.md)
- Source domain: https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps
