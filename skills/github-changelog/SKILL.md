---
name: github-changelog
description: >
  Use this skill when the user wants to know what GitHub announced or released on a specific date,
  wants to review GitHub Changelog entries, needs a summary of recent GitHub features or changes,
  wants to build action plans based on GitHub product updates, or mentions "GitHub Changelog,"
  "GitHub release notes," "what did GitHub ship," or "GitHubの更新." Trigger whenever the user
  asks about GitHub feature announcements, product updates, or wants to understand what changed in
  GitHub on a particular day. This skill fetches all posts from the GitHub Changelog RSS feed for a
  specified date and organizes them into summaries, action plans, and references (URLs).
---

# GitHub Changelog Skill

指定された日付の GitHub Changelog 投稿をすべて取得し、各エントリを **要約・アクションプラン・リファレンス** に整理します。

## Quick Reference

| タスク | コマンド |
|--------|---------|
| 指定日の投稿を取得 | `python skills/github-changelog/scripts/fetch_changelog.py <YYYY-MM-DD>` |

---

## Workflow

1. ユーザーから対象日付（例: `2026-03-06`）を確認する。指定がなければ今日の日付を使用する。
2. スクリプトを実行して RSS フィードから該当エントリの生データを取得する。
3. 取得した各エントリに対して以下を生成する：
   - **要約**: 何が変わったか・追加されたかを 2〜3 文で簡潔に説明する
   - **ユーザーにとって良いこと**: 変更が GitHub ユーザーにどのようなメリットをもたらすかを説明する
   - **アクションプラン**: 開発者やチームが取るべき具体的な次のステップ
   - **リファレンス**: 元の Changelog エントリへのリンク
4. 結果を以下の出力フォーマットで整形して返す。

---

## Running the Script

```bash
python skills/github-changelog/scripts/fetch_changelog.py <YYYY-MM-DD>
```

スクリプトは `https://github.blog/changelog/feed/` から RSS フィードを取得し、
`pubDate` が指定日に一致するエントリを抽出して JSON 配列として標準出力に返す。

**例:**
```bash
python skills/github-changelog/scripts/fetch_changelog.py 2026-03-06
```

スクリプト出力例（JSON）:
```json
[
  {
    "title": "GitHub Actions: New feature X",
    "link": "https://github.blog/changelog/2026-03-06-...",
    "date": "2026-03-06",
    "description": "Raw HTML/text description from feed"
  }
]
```

---

## Output Format

結果は必ず次のテンプレートで出力する: `./assets/template.md`

エントリが 0 件の場合は「<date> のエントリは見つかりませんでした」と伝え、前後の日付で再確認することを提案する。

---

## Notes on Action Plans

アクションプランを書くときのガイドライン:

- **具体的に**: 「確認してください」ではなく「`.github/workflows/` の `uses:` バージョンを更新してください」のように書く
- **影響範囲を明示**: 変更が全リポジトリに自動適用されるのか、設定が必要なのかを必ず示す
- **破壊的変更は目立たせる**: ⚠️ を使い、移行期限があれば日付を添える
- **非エンジニア向け変更も含める**: UI 変更や設定変更など、開発者以外に影響するものも漏らさない
