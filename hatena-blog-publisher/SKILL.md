---
name: hatena-blog-publisher
description: >
  Use this skill when the user wants to publish a local file as a Hatena Blog entry,
  create a Hatena Blog draft from Markdown or text, or automate Hatena Blog posting
  with the AtomPub API. Trigger when the user mentions はてなブログ, Hatena Blog,
  posting a file as an article, creating a blog draft, or asks to run the bundled
  publisher script. The script reads the first line as the article title and sends
  the complete file content as Markdown.
license: Proprietary. LICENSE has complete terms.
---

# Hatena Blog Publisher

指定されたローカルファイルの内容を、はてなブログの記事として投稿するスキル。
現在のスクリプトは公開せず、常に下書きとして投稿する。

## 前提条件

- .NET 10 SDK 以降（C# file-based app の実行に使用）
- はてなブログのブログID
- はてなブログAtomPub APIで利用するユーザー名とAPIキー
- 投稿対象ファイルへの読み取りアクセス

認証情報はソースコードに埋め込まず、スクリプトの入力パラメーターとして渡す。

| パラメーター | 内容 | 例 |
| --- | --- | --- |
| `username` | はてなユーザー名 | `example` |
| `api-key` | はてなブログAPIキー | `...` |
| `blog-id` | ブログID（通常はドメイン） | `example.hatenablog.com` |
| `file-path` | 投稿対象ファイルのパス | `articles\2026-09-05.md` |

## 実行方法

スキルディレクトリを基準に、次のコマンドで実行する。

```powershell
dotnet run --file scripts\hatena-client.cs -- <username> <api-key> <blog-id> <file-path>
```

例:

```powershell
dotnet run --file scripts\hatena-client.cs -- example your-api-key example.hatenablog.com articles\2026-09-05.md
```

## 入力ファイルの扱い

- ファイルが存在しない場合はエラーを標準エラー出力に表示して終了する。
- 先頭行を記事タイトルとして使用する。
- 先頭行の `#` は取り除き、前後の空白を除去する。したがって、Markdownの
  `# タイトル` を先頭行に置ける。
- ファイル全体を記事本文として送信する。本文のMarkdown記法は保持される。
- 空行や本文中の見出しは変更しない。

推奨する入力例:

```markdown
# はてなブログの記事タイトル

ここから記事本文です。

## セクション

本文をMarkdownで記述します。
```

## 投稿仕様

1. `https://blog.hatena.ne.jp/{username}/{blogId}/atom/entry` にAtomPub APIで
   `POST` する。
2. Basic認証に `username` と `api-key` を使用する。
3. Atomエントリの本文タイプは `text/markdown` にする。
4. `app:draft=yes` を付けるため、投稿結果は下書きになる。
5. カテゴリは現在指定しない。

実行時には送信するXMLとHTTPステータス、APIレスポンス本文が標準出力に表示される。
APIキー自体はXML本文に含まれないが、記事本文が表示されるため、共有端末やCIログで
実行する場合はログの取り扱いに注意する。

## ワークフロー

1. 投稿するファイルのパスと、先頭行が意図したタイトルになっていることを確認する。
2. `username`、`api-key`、`blog-id` を準備する。APIキーはシェル履歴やCIログに
   残る可能性があるため、必要に応じて安全な実行環境から渡す。
3. 上記のコマンドでスクリプトを実行する。
4. HTTPステータスとレスポンス本文を確認し、はてなブログ管理画面で下書きを確認する。
5. 内容・タイトル・公開設定を確認してから、管理画面で公開する。

## 関連ファイル

- 実行スクリプト: `scripts\hatena-client.cs`
- C# file-based app の実行方法: [csharp-file-based-apps](../csharp-file-based-apps/SKILL.md)