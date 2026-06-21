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

最新の .NET における **C# File-based apps** を扱うためのスキルです。`dotnet run app.cs` / `dotnet app.cs` のような単一 `.cs` ファイル実行、`#:` ディレクティブ、publish / pack / convert、launch profile、user secrets、build cache、配置指針まで一貫して扱います。

## Skill directory

`~/.copilot/skills/csharp-file-based-apps/`

## Quick Reference

| タスク | コマンド |
|--------|---------|
| 参照トピック一覧 | `python scripts/file_based_apps_reference.py list` |
| トピック詳細表示 | `python scripts/file_based_apps_reference.py show directives` |
| サンプル生成 | `python scripts/file_based_apps_reference.py example web` |

---

## Workflow

1. まずユーザーが求めているのが **新規の file-based app 作成**、**既存スクリプトの改善**、**従来の .csproj との変換・比較**、**トラブルシュート** のどれかを判断する。
2. `references/file-based-apps.md` を参照し、使えるディレクティブ・CLI・制約を確認する。細かい例が必要なら Python スクリプトで該当トピックやサンプルを引く。
3. 提案や生成を行うときは、以下を明確にする。
   - 対象 SDK と前提（**.NET 10 SDK 以降**。`#:include` は SDK 10.0.300+ / .NET 11 Preview 3+）
   - どの `#:` ディレクティブを使うべきか
   - `dotnet run` / `build` / `publish` / `pack` / `restore` / `project convert` のどれが適切か
   - 既存の `Directory.Build.props` や `global.json` など暗黙の影響がないか
4. 実装例を出すときは **単一ファイルで成立する最小例** を優先し、追加ファイルや変換が必要な場合だけ理由付きで広げる。
5. 断定してよいのは Microsoft Learn で確認できる内容に限る。プレビュー要素や OS 差分は明示する。

---

## What to help with

- 単一 `.cs` ファイルでの実行・ビルド・配布
- `#:` ディレクティブの使い分け
- NuGet パッケージ参照、別プロジェクト参照、MSBuild property の設定
- Native AOT の既定挙動と無効化
- .NET tool としての pack
- `dotnet project convert` による従来プロジェクト化
- `app.run.json` を使った launch profile
- `dotnet user-secrets ... --file app.cs`
- build cache や配置ミスによるハマりどころ

---

## Supported directives

File-based apps では、C# ファイル先頭に `#:` で始まるディレクティブを置く。

| Directive | 用途 | 例 |
|-----------|------|----|
| `#:package` | NuGet パッケージ参照 | `#:package Spectre.Console@*` |
| `#:property` | MSBuild property 設定 | `#:property PublishAot=false` |
| `#:project` | 別プロジェクト参照 | `#:project ../Shared/Shared.csproj` |
| `#:sdk` | 使用 SDK 指定 | `#:sdk Microsoft.NET.Sdk.Web` |
| `#:include` | 追加ファイル取り込み | `#:include shared/**/*.cs` |

### Guidance

- 使えないディレクティブを作らない。使うのは上の 5 種類だけ。
- `#:package` は、中央管理がない限りバージョン明示を優先する。最新版追従なら `@*` を使う。
- `#:include` で取り込む `.cs` ファイルには **top-level statements を置けない**。型・メソッド・namespace などを定義する。
- `#:property` では MSBuild property function や環境変数参照も使えるが、複雑にしすぎず目的を説明する。
- ASP.NET Core 系や設定ファイル込みの例では `#:sdk Microsoft.NET.Sdk.Web` を検討する。

---

## CLI patterns

### Run

```bash
dotnet run app.cs
dotnet app.cs
dotnet run app.cs -- arg1 arg2
```

- カレントディレクトリに `.csproj` がある場合、`dotnet run app.cs` は互換性のため **そのプロジェクトを実行して `app.cs` を引数として渡す** ことがある。曖昧さを避けたいときは `--file` を使う。

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

- `publish` は既定で Native AOT を有効にする。
- `pack` は既定で `PackAsTool=true`。
- 既定挙動を変えたい場合は `#:property PublishAot=false` や `#:property PackAsTool=false` を使う。

---

## Troubleshooting checklist

### 1. 実行したいのに既存プロジェクトが走る

- `dotnet run --file app.cs` を使う。
- `app.cs` を `.csproj` 配下から分離したディレクトリへ移すことも検討する。

### 2. キャッシュが怪しい

- `dotnet clean app.cs`
- `dotnet clean file-based-apps`
- 必要に応じて `dotnet build app.cs` → `dotnet run app.cs --no-build`

### 3. implicit build files の影響が強い

- `Directory.Build.props`
- `Directory.Build.targets`
- `Directory.Packages.props`
- `nuget.config`
- `global.json`

上記が親ディレクトリにあると file-based app にも効く。予期しない挙動の原因になりやすい。

### 4. 配置が悪い

- `.csproj` の cone の中に utility 用 `app.cs` を置かない。
- scripts 用ディレクトリを分離する。

---

## Output expectations

ユーザーへの回答では、必要に応じて次の順で整理する。

1. **最短の結論**: 何を実行・追加すればよいか
2. **サンプルコード**: 単一 `.cs` ファイルで動く最小例
3. **補足**: SDK 要件、プレビュー制約、キャッシュやフォルダ配置の注意

変換や設計判断が絡む場合は、file-based app を選ぶ理由と通常の `.csproj` に戻すべき条件も明示する。

---

## Examples

### Minimal console example

```csharp
#:package Spectre.Console@*

using Spectre.Console;

AnsiConsole.MarkupLine("[green]Hello from a file-based app[/]");
```

実行:

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

- 詳細リファレンス: [references/file-based-apps.md](./references/file-based-apps.md)
- 公式ドメイン参照先: https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps
