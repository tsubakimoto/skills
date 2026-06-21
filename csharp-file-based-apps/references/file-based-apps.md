# File-based apps reference

Source: https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps

This reference reorganizes the Microsoft Learn **File-based apps** article into a format that is easier to use inside the skill. If you need to confirm the latest behavior, check the source article as well.

## Applies to

- **.NET 10 SDK and later**
- `#:include` is available in **.NET 11 Preview 3 / .NET SDK 10.0.300+**

## Core idea

File-based apps let you build, run, publish, and pack a **single C# file** without first creating a traditional `.csproj`.

Good fits include:

- Small utilities that are more than throwaway snippets
- Tools you want to distribute as a single file or lightweight app
- Sample code and experimental apps
- Small applications where creating a full project would add unnecessary overhead

## Supported directives

### `#:package`

Adds a NuGet package reference.

```csharp
#:package Newtonsoft.Json@13.0.3
#:package Spectre.Console@*
```

Notes:

- Omitting a version is reliable only when central package management (`Directory.Packages.props`) is in use
- Otherwise, specify a version explicitly or use `@*`

### `#:property`

Sets an MSBuild property.

```csharp
#:property TargetFramework=net10.0
#:property PublishAot=false
```

Environment variables and property functions are also supported:

```csharp
#:property LogLevel=$([MSBuild]::ValueOrDefault('$(LOG_LEVEL)', 'Information'))
```

### `#:project`

References another project.

```csharp
#:project ../SharedLibrary/SharedLibrary.csproj
```

### `#:sdk`

Switches the SDK. The default is `Microsoft.NET.Sdk`.

```csharp
#:sdk Microsoft.NET.Sdk.Web
```

### `#:include`

Includes extra files.

```csharp
#:include helpers.cs
#:include shared/**/*.cs
```

Notes:

- `.cs` → `Compile`
- `.resx` → `EmbeddedResource`
- `.json` → `None`
- `.razor` → `Content`
- Included `.cs` files can't contain top-level statements
- Using globs currently disables build caching

## CLI commands

### Run

```bash
dotnet run --file app.cs
dotnet run app.cs
dotnet app.cs
```

Pass arguments:

```bash
dotnet run app.cs -- arg1 arg2
```

Run from stdin:

```powershell
'Console.WriteLine("hello from stdin!");' | dotnet run -
```

Notes:

- If a project file exists in the current directory, `dotnet run app.cs` might run that project and pass `app.cs` as an argument
- Prefer `--file` when you need to target the file-based app explicitly

### Build

```bash
dotnet build app.cs
```

- The default output path is under the system temp directory
- Use `--output` for a custom location
- Set `#:property OutputPath=./output` to change the default output path

### Clean

```bash
dotnet clean app.cs
dotnet clean file-based-apps
```

- The second command clears the file-based apps cache

### Publish

```bash
dotnet publish app.cs
```

- **Native AOT is enabled by default**
- Disable it with `#:property PublishAot=false`
- By default, an `artifacts` directory is created next to the `.cs` file

### Pack

```bash
dotnet pack app.cs
```

- **PackAsTool=true is the default**
- Use `#:property PackAsTool=false` if you don't want a .NET tool package

### Convert

```bash
dotnet project convert app.cs
```

- Leaves the original `.cs` file untouched and creates a new directory with a `.csproj` and copied `.cs` file

### Restore

```bash
dotnet restore app.cs
```

- Restore usually happens implicitly during build and run

## User secrets

```bash
dotnet user-secrets set "ApiKey" "your-secret-value" --file app.cs
dotnet user-secrets list --file app.cs
```

- The user secrets ID is stabilized from the full file path
- `list` prints secret values, so avoid it in public contexts

## Launch profiles

File-based apps can use **`[ApplicationName].run.json`** in the same directory, in addition to `Properties/launchSettings.json`.

Example: `app.run.json` for `app.cs`

```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

Selection priority:

1. `--launch-profile`
2. `DOTNET_LAUNCH_PROFILE`
3. The first profile defined in the file

The traditional `Properties/launchSettings.json` file is also supported, and it takes priority when both formats exist.

## Shell execution

On Unix-like systems, you can run the file directly with a shebang.

```csharp
#!/usr/bin/env -S dotnet --
#:package Spectre.Console@*

using Spectre.Console;

AnsiConsole.MarkupLine("[green]Hello, World![/]");
```

Notes:

- Executable permission is required
- Use **LF line endings** and no BOM with a shebang
- `--` prevents `dotnet` from consuming arguments meant for the app

## Implicit build files

These files in parent directories can affect a file-based app:

- `Directory.Build.props`
- `Directory.Build.targets`
- `Directory.Packages.props`
- `nuget.config`
- `global.json`

When behavior is hard to explain, check whether these files exist and what they contain.

## Build caching

The cache key is broadly influenced by:

- Source contents
- Directive configuration
- SDK version
- The presence and contents of implicit build files

Common gotchas:

- Changes to implicit build files might not look like they triggered a rebuild
- Running the same file-based app concurrently can cause build output contention

If you need concurrent runs:

```bash
dotnet build app.cs
dotnet run app.cs --no-build
```

## Folder layout recommendations

### Avoid project file cones

Avoid placing file-based apps inside the subtree of a `.csproj`.

Not recommended:

```text
MyProject/
├── MyProject.csproj
├── Program.cs
└── scripts/
    └── utility.cs
```

Recommended:

```text
MyProject/
├── MyProject.csproj
└── Program.cs

scripts/
└── utility.cs
```

### Be mindful of implicit files

If different file-based apps need different build conditions, isolate them under different parent directories so implicit files don't leak across them.
