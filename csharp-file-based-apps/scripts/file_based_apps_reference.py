#!/usr/bin/env python3
"""Quick reference helper for the csharp-file-based-apps skill."""

from __future__ import annotations

import argparse
import sys


TOPICS = {
    "overview": """File-based apps are single-file C# applications supported by the .NET 10 SDK and later.
Use them when you want to run, build, publish, or pack a .cs file without creating a .csproj first.

Key defaults:
- Native AOT is enabled by default for publish.
- PackAsTool is enabled by default for pack.
- Implicit files such as Directory.Build.props and global.json still affect the app.
""",
    "directives": """Supported directives:
- #:package <PackageId@Version>  -> add a NuGet package reference
- #:property Name=Value          -> set an MSBuild property
- #:project path/to/project      -> reference another project
- #:sdk Microsoft.NET.Sdk.Web    -> switch SDK
- #:include pattern              -> include extra files (.NET SDK 10.0.300+ / .NET 11 Preview 3+)

Important caveats:
- Included .cs files can add types or methods, but not top-level statements.
- Omitting a package version is reliable only with central package management.
- Glob patterns in #:include currently disable build caching.
""",
    "cli": """Core commands:
- dotnet run --file app.cs
- dotnet run app.cs -- arg1 arg2
- dotnet app.cs
- dotnet build app.cs
- dotnet clean app.cs
- dotnet clean file-based-apps
- dotnet publish app.cs
- dotnet pack app.cs
- dotnet project convert app.cs
- dotnet restore app.cs

Tip:
If a .csproj exists in the current directory, prefer --file so dotnet doesn't run the project by mistake.
""",
    "launch-profiles": """File-based apps can use app.run.json next to app.cs.

Profile selection priority:
1. --launch-profile
2. DOTNET_LAUNCH_PROFILE
3. First profile in the JSON file

Traditional Properties/launchSettings.json is also supported and takes priority if both exist.
""",
    "caching": """Build caching depends on source contents, directives, SDK version, and implicit build files.

Troubleshooting steps:
1. dotnet clean app.cs
2. dotnet clean file-based-apps
3. dotnet build app.cs
4. dotnet run app.cs --no-build

Avoid concurrent builds of the same file-based app unless you've built first and run with --no-build.
""",
    "layout": """Recommended layout:
- Keep file-based apps outside .csproj directory cones.
- Isolate them from parent Directory.Build.props / targets when different settings are needed.
- Be aware that Directory.Packages.props, nuget.config, and global.json still apply from parent directories.
""",
}


EXAMPLES = {
    "minimal": """#:package Spectre.Console@*

using Spectre.Console;

AnsiConsole.MarkupLine("[green]Hello from a file-based app[/]");
""",
    "web": """#:sdk Microsoft.NET.Sdk.Web

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { message = "hello" }));

app.Run();
""",
    "property": """#:property PublishAot=false
#:property OutputPath=./output

Console.WriteLine("Quick utility");
""",
    "include": """#:include helpers.cs
#:include shared/**/*.cs

Console.WriteLine(FormatMessage("hello"));
""",
}


def cmd_list() -> int:
    print("Topics:")
    for name in TOPICS:
        print(f"- {name}")
    print("\nExamples:")
    for name in EXAMPLES:
        print(f"- {name}")
    return 0


def cmd_show(topic: str) -> int:
    content = TOPICS.get(topic)
    if content is None:
        print(f"Unknown topic: {topic}", file=sys.stderr)
        return 1
    print(content.strip())
    return 0


def cmd_example(name: str) -> int:
    content = EXAMPLES.get(name)
    if content is None:
        print(f"Unknown example: {name}", file=sys.stderr)
        return 1
    print(content.rstrip())
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Quick reference helper for .NET file-based apps."
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    subparsers.add_parser("list", help="List available topics and examples.")

    show_parser = subparsers.add_parser("show", help="Show a named topic.")
    show_parser.add_argument("topic", choices=sorted(TOPICS))

    example_parser = subparsers.add_parser("example", help="Show a code example.")
    example_parser.add_argument("name", choices=sorted(EXAMPLES))

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    if args.command == "list":
        return cmd_list()
    if args.command == "show":
        return cmd_show(args.topic)
    if args.command == "example":
        return cmd_example(args.name)

    parser.error(f"Unhandled command: {args.command}")
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
