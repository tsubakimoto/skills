# File-based apps reference

Source: https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps

このリファレンスは Microsoft Learn の **File-based apps** 記事をもとに、Skill で参照しやすいように整理したものです。最新仕様の確認が必要なときは元記事も見ること。

## Applies to

- **.NET 10 SDK and later**
- `#:include` は **.NET 11 Preview 3 / .NET SDK 10.0.300+** で利用可能

## Core idea

File-based apps は、従来の `.csproj` を作らず **単一の C# ファイル** から build / run / publish / pack できる仕組み。

向いている用途:

- 使い捨てではないが小さいユーティリティ
- 単一ファイルで配布したいツール
- サンプルコードや検証用アプリ
- プロジェクトの作成コストを避けたい小規模アプリ

## Supported directives

### `#:package`

NuGet パッケージ参照を追加する。

```csharp
#:package Newtonsoft.Json@13.0.3
#:package Spectre.Console@*
```

補足:

- バージョン省略は、中央パッケージ管理 (`Directory.Packages.props`) がある場合のみ確実
- それ以外はバージョンを明示するか `@*` を使う

### `#:property`

MSBuild property を設定する。

```csharp
#:property TargetFramework=net10.0
#:property PublishAot=false
```

環境変数や property function も利用可能:

```csharp
#:property LogLevel=$([MSBuild]::ValueOrDefault('$(LOG_LEVEL)', 'Information'))
```

### `#:project`

別プロジェクトを参照する。

```csharp
#:project ../SharedLibrary/SharedLibrary.csproj
```

### `#:sdk`

使用する SDK を切り替える。既定は `Microsoft.NET.Sdk`。

```csharp
#:sdk Microsoft.NET.Sdk.Web
```

### `#:include`

追加ファイルを取り込む。

```csharp
#:include helpers.cs
#:include shared/**/*.cs
```

補足:

- `.cs` → `Compile`
- `.resx` → `EmbeddedResource`
- `.json` → `None`
- `.razor` → `Content`
- 取り込まれる `.cs` ファイルに top-level statements は書けない
- glob を使うと現在は build cache が無効化される

## CLI commands

### Run

```bash
dotnet run --file app.cs
dotnet run app.cs
dotnet app.cs
```

引数を渡す:

```bash
dotnet run app.cs -- arg1 arg2
```

stdin から実行:

```powershell
'Console.WriteLine("hello from stdin!");' | dotnet run -
```

注意:

- カレントディレクトリに project file がある場合、`dotnet run app.cs` はその project を実行して `app.cs` を引数として渡すことがある
- file-based app を確実に指したいときは `--file` を優先する

### Build

```bash
dotnet build app.cs
```

- 既定出力先は temp 配下
- 任意の出力先が必要なら `--output`
- 既定の出力先を変えるなら `#:property OutputPath=./output`

### Clean

```bash
dotnet clean app.cs
dotnet clean file-based-apps
```

- 後者は file-based apps 用キャッシュをまとめて掃除する

### Publish

```bash
dotnet publish app.cs
```

- **Native AOT は既定で有効**
- 無効化するなら `#:property PublishAot=false`
- 既定では `.cs` ファイルの隣に `artifacts` ディレクトリが作られる

### Pack

```bash
dotnet pack app.cs
```

- **PackAsTool=true が既定**
- .NET tool にしたくないなら `#:property PackAsTool=false`

### Convert

```bash
dotnet project convert app.cs
```

- 元の `.cs` はそのまま残しつつ、新しいディレクトリに `.csproj` とコピーされた `.cs` を生成する

### Restore

```bash
dotnet restore app.cs
```

- 通常は build / run 時に暗黙 restore される

## User secrets

```bash
dotnet user-secrets set "ApiKey" "your-secret-value" --file app.cs
dotnet user-secrets list --file app.cs
```

- user secrets ID はファイルのフルパス由来で安定化される
- `list` は値を出力するので公開文脈で使わない

## Launch profiles

file-based apps では `Properties/launchSettings.json` に加えて、同じディレクトリに **`[ApplicationName].run.json`** を置ける。

例: `app.cs` に対して `app.run.json`

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

選択優先順位:

1. `--launch-profile`
2. `DOTNET_LAUNCH_PROFILE`
3. 定義順の最初の profile

従来の `Properties/launchSettings.json` も使えるが、両方あると従来形式が優先される。

## Shell execution

Unix 系では shebang で直接実行できる。

```csharp
#!/usr/bin/env -S dotnet --
#:package Spectre.Console@*

using Spectre.Console;

AnsiConsole.MarkupLine("[green]Hello, World![/]");
```

注意:

- 実行権限が必要
- shebang を使うなら **LF 改行**、BOM なし
- `--` はアプリ引数を `dotnet` に食われないようにするため

## Implicit build files

親ディレクトリの次のファイルは file-based app に影響する:

- `Directory.Build.props`
- `Directory.Build.targets`
- `Directory.Packages.props`
- `nuget.config`
- `global.json`

問題が再現しづらいときは、これらの有無と内容を確認する。

## Build caching

キャッシュのキーはおおむね次で決まる:

- ソース内容
- ディレクティブ構成
- SDK バージョン
- implicit build files の存在と内容

ハマりどころ:

- implicit build files の変更で期待通り再ビルドされないように見えることがある
- 同じ file-based app を並列起動すると build 出力の競合でエラーになることがある

並列実行したい場合:

```bash
dotnet build app.cs
dotnet run app.cs --no-build
```

## Folder layout recommendations

### Avoid project file cones

`.csproj` 配下の subtree に file-based app を置かないほうがよい。

悪い例:

```text
MyProject/
├── MyProject.csproj
├── Program.cs
└── scripts/
    └── utility.cs
```

良い例:

```text
MyProject/
├── MyProject.csproj
└── Program.cs

scripts/
└── utility.cs
```

### Be mindful of implicit files

file-based apps ごとに違う build 条件が欲しいなら、影響を受ける親ディレクトリを分ける。
