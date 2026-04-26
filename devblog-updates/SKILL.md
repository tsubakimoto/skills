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
  action plans, and references.
license: Proprietary. LICENSE has complete terms.
---

# Microsoft Developer Blogs Skill

指定された日付の Microsoft Developer Blogs 投稿をすべて取得し、各エントリを **要約・開発者への影響・アクションプラン・リファレンス** に整理します。

## Skill directory

`~/.copilot/skills/devblog-updates/`

## Quick Reference

| タスク | コマンド |
|--------|---------|
| 指定日の投稿を取得 | `python scripts/fetch_devblog_updates.py <YYYY-MM-DD>` |

---

## Workflow

1. ユーザーから対象日付（例: `2026-04-08`）を確認する。指定がなければ今日の日付を使用する。
2. スクリプトを実行して Microsoft Developer Blogs の RSS フィードから該当日の生データを取得する。
3. 取得した各エントリに対して以下を生成する：
   - **要約**: 何が発表・更新されたのかを 2〜3 文で簡潔に説明する
   - **開発者にとって重要なこと**: どの製品・開発フロー・チームに関係するかを説明する
   - **アクションプラン**: 開発者やチームが取るべき具体的な次のステップ
   - **リファレンス**: 元の投稿へのリンク
4. DevBlogs の投稿は短い告知や全文の一部だけがフィードに載ることがある。description と URL から読み取れる範囲で整理し、本文にない詳細を断定しない。
5. 結果を以下の出力フォーマットで整形して返す。

---

## Running the Script

```bash
python scripts/fetch_devblog_updates.py <YYYY-MM-DD>
```

スクリプトは `https://devblogs.microsoft.com/landing` から RSS フィードを取得し、
`pubDate` が指定日に一致するエントリを抽出して JSON 配列として標準出力に返す。

**例:**
```bash
python scripts/fetch_devblog_updates.py 2026-04-08
```

スクリプト出力例（JSON）:
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
    "description": "Learn what's new in Visual Studio Code 1.115 Read the full article"
  }
]
```

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
