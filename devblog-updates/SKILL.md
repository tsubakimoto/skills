---
name: devblog-updates
description: >
  Use this skill when the user wants to know what Microsoft announced for developers on a
  specific date, wants to review Microsoft Developer Blogs posts, needs a roundup of recent
  updates from Visual Studio, VS Code, Azure, .NET, Go, or other Microsoft engineering blogs,
  or asks what changed on Microsoft DevBlogs on a particular day. Trigger whenever the user
  mentions "Microsoft Developer Blogs," "Microsoft DevBlogs," "devblogs.microsoft.com," or asks
  for daily Microsoft developer news. This skill fetches all posts from the Microsoft Developer
  Blogs RSS feed for a specified date and organizes each entry into summaries, developer impact,
  action plans, and references. Posts are collected from both the landing aggregate feed and the
  per-blog WordPress REST APIs so that entries outside the feed's 25-item window are not missed.
license: Proprietary. LICENSE has complete terms.
---

# Microsoft Developer Blogs Skill

指定された日付の Microsoft Developer Blogs 投稿をすべて取得し、各エントリを **要約・開発者への影響・アクションプラン・リファレンス** に整理します。

## Skill directory

`~/.copilot/skills/devblog-updates/`

## Quick Reference

| タスク | コマンド |
|--------|---------|
| 指定日の投稿を取得 | `dotnet run --file scripts\fetch_devblog_updates.cs -- <YYYY-MM-DD>` |
| 期間で取得 | `dotnet run --file scripts\fetch_devblog_updates.cs -- <FROM> --to <TO>` |
| 日本時間で日付を区切る | `dotnet run --file scripts\fetch_devblog_updates.cs -- <YYYY-MM-DD> --tz +09:00` |
| 取得元の内訳を確認 | `dotnet run --file scripts\fetch_devblog_updates.cs -- <YYYY-MM-DD> --diagnostics` |

---

## Workflow

1. ユーザーから対象日付（例: `2026-04-08`）を確認する。指定がなければ今日の日付を使用する。
2. スクリプトを実行して Microsoft Developer Blogs の各ブログから該当日の生データを取得する。
3. 取得した各エントリに対して以下を生成する：
   - **要約**: 何が発表・更新されたのかを 2〜3 文で簡潔に説明する
   - **開発者にとって重要なこと**: どの製品・開発フロー・チームに関係するかを説明する
   - **アクションプラン**: 開発者やチームが取るべき具体的な次のステップ
   - **リファレンス**: 元の投稿へのリンク
4. DevBlogs の投稿は短い告知や全文の一部だけが description に載ることがある。description と URL から読み取れる範囲で整理し、本文にない詳細を断定しない。
5. 結果を以下の出力フォーマットで整形して返す。

> 件数が想定より少ないと感じた場合は `--diagnostics` を付けて再実行し、取得元ごとの成否を確認する。

---

## Running the Script

```bash
dotnet run --file scripts\fetch_devblog_updates.cs -- <YYYY-MM-DD> [--to <YYYY-MM-DD>] [オプション]
```

### 取得の仕組み

スクリプトは **2 系統のデータソース** を並行して取得し、リンク単位で重複排除する。

1. **ランディング集約フィード** — `https://devblogs.microsoft.com/landing`
   全ブログ横断だが **常に最新 25 件のみ**（およそ直近 4〜5 日分）を返し、ページングも効かない。
2. **各ブログの WordPress REST API** — `https://devblogs.microsoft.com/<slug>/wp-json/wp/v2/posts`
   ブログ一覧は `robots.txt` の Sitemap 行と `/wp-json/custom/v1/all-blogs` から自動探索する（約 75 ブログ）。
   こちらは日付範囲を指定でき、古い日付も取得できる。

> ⚠️ 旧バージョンはランディングフィードのみを見ていたため、25 件の窓から溢れた投稿を必ず取りこぼしていた。REST API 併用でこの問題を解消している。

### オプション

| オプション | 既定値 | 説明 |
|-----------|--------|------|
| `--to <YYYY-MM-DD>` | 開始日と同じ | 終了日（両端を含む） |
| `--tz <±HH:MM>` | `+00:00` | 日付の区切りに使うタイムゾーン。JST 基準なら `+09:00` |
| `--blogs <slug,...>` | 全ブログ | 取得対象のブログを限定する |
| `--max-pages <n>` | `3` | ブログあたりの REST ページ取得上限 |
| `--concurrency <n>` | `8` | 同時リクエスト数 |
| `--max-description <n>` | `1200` | description の最大文字数 |
| `--diagnostics` | 無効 | 取得元ごとの成否を標準エラーに出力 |

**例:**
```bash
dotnet run --file scripts\fetch_devblog_updates.cs -- 2026-04-08
dotnet run --file scripts\fetch_devblog_updates.cs -- 2026-04-01 --to 2026-04-08 --tz +09:00
```

スクリプト出力例（JSON、標準出力）:
```json
[
  {
    "title": "Visual Studio Code 1.115",
    "link": "https://devblogs.microsoft.com/vscode-blog/visual-studio-code-1.115",
    "date": "2026-04-08",
    "published_at": "2026-04-08T17:00:00Z",
    "blog": "VS Code Blog",
    "blog_slug": "vscode-blog",
    "author": "Visual Studio Code Team",
    "description": "Learn what's new in Visual Studio Code 1.115 Read the full article",
    "source": "landing-feed+rest:vscode-blog"
  }
]
```

`source` は取得元。`landing-feed` / `rest:<slug>` が `+` で連結される。

### 既知の制約

- `vscode-blog`、`external-blogs`、`xamarin`、`ericlippert` などは REST API が無効化されており、
  **ランディングフィードの窓（直近 4〜5 日）内でしか取得できない**。これより古い日付では取りこぼす。
  `--diagnostics` を付けると `SKIP <slug>: no REST API (landing feed only)` として一覧表示される。
- ランディングフィードは数時間〜1 日程度の反映ラグがある。当日分を照会したときに件数が少ない場合は、
  翌日以降に再実行すると増えることがある。
- `aspnet` / `nuget` などは `dotnet` へリダイレクトされるが、リンク単位の重複排除で吸収される。
- 同じ告知が複数ブログに転載されている場合（`vscode-blog` と `external-blogs` など）は、
  タイトルと日付が一致するものを 1 件に統合する。`external-blogs` は転載元として扱い、正規側を採用する。

---

## Output Format

結果は必ず次のテンプレートで出力する: `~/.copilot/skills/devblog-updates/assets/template.md`

エントリが 0 件の場合は「<date> の Microsoft Developer Blogs エントリは見つかりませんでした」と伝え、前後の日付で再確認することを提案する。

---

## Notes on Summaries and Action Plans

アクションプランを書くときのガイドライン:

- **対象ブログを明示する**: `blog` を見て、VS Code / Azure / .NET などどの領域の更新かを最初に示す
- **推測を足さない**: RSS description が短い場合は、分からない点を埋めずに「詳細は本文参照」と明記する
- **具体的に書く**: 「確認してください」ではなく「利用中の VS Code 拡張やワークフローへの影響を確認する」のように書く
- **影響範囲を明示する**: 自分の環境だけで済むか、チーム全体の設定変更が必要かを示す
- **破壊的変更や更新作業は目立たせる**: ⚠️ を使って移行・アップデートの必要性を分かりやすく伝える
