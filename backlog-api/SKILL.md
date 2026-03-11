---
name: backlog-api
description: "Backlog（ヌーラボ社のプロジェクト管理SaaS）のREST API v2に関するエキスパートスキル。課題の作成・検索・更新、プロジェクト管理、Wiki操作、Git/プルリクエスト、通知、チーム管理などBacklog APIを使ったコード生成や自動化を支援する。Backlog APIの認証方式、エンドポイント、パラメータ、エラーハンドリングについて質問されたとき、またはBacklog連携スクリプトやアプリを構築するときに使用する。"
---

# Backlog API Expert

Backlog API v2 を使った開発を支援するスキル。

## API概要

- **ベースURL**: `https://{スペースID}.backlog.com/api/v2` または `https://{スペースID}.backlog.jp/api/v2`
- **形式**: REST API（JSON）
- **公式ドキュメント**: https://developer.nulab.com/ja/docs/backlog/
- **詳細なAPI仕様**: [references/backlog-v2-openapi.yml](./references/backlog-v2-openapi.yml) を参照

## 前提条件

- Backlogスペースへのアクセス権
- API Key（スペースの「個人設定」>「API」から発行）または OAuth 2.0 アクセストークン
- Python: `requests` パッケージ（`pip install requests`）

## 認証

### API Key認証（クエリパラメータ）

```
GET /api/v2/space?apiKey=YOUR_API_KEY
```

### OAuth 2.0（Authorizationヘッダー）

```
Authorization: Bearer YOUR_ACCESS_TOKEN
```

## APIカテゴリ

| カテゴリ | ベースパス | 主な操作 |
|---------|-----------|---------|
| スペース | `/space` | 情報取得、お知らせ、容量、添付ファイル送信 |
| ユーザー | `/users` | CRUD、認証ユーザー情報、活動履歴、スター |
| チーム | `/teams` | CRUD、アイコン（`/groups` は非推奨） |
| プロジェクト | `/projects` | CRUD、状態・種別・カスタム属性、Webhook |
| 課題 | `/issues` | CRUD、検索、コメント、添付ファイル |
| Wiki | `/wikis` | CRUD、タグ、添付ファイル、更新履歴 |
| Git | `/projects/.../git/repositories` | リポジトリ、プルリクエスト |
| お知らせ | `/notifications` | 一覧取得、既読化 |
| マスターデータ | `/priorities`, `/resolutions` | 優先度・完了理由一覧 |

各エンドポイントの詳細パラメータは [references/backlog-v2-openapi.yml](./references/backlog-v2-openapi.yml) を参照。

## 課題検索パラメータ（`GET /issues`）

| パラメータ | 説明 |
|-----------|------|
| `projectId[]` | プロジェクトID（複数指定可） |
| `statusId[]` | 状態ID（複数指定可） |
| `assigneeId[]` | 担当者ID（複数指定可） |
| `issueTypeId[]` | 種別ID（複数指定可） |
| `keyword` | 検索キーワード |
| `sort` | ソート属性（`created`, `updated`, `dueDate` 等） |
| `order` | `asc` / `desc`（デフォルト `desc`） |
| `count` | 取得上限 1-100（デフォルト 20） |
| `offset` | オフセット |
| `createdSince` / `createdUntil` | 登録日範囲 (yyyy-MM-dd) |
| `parentChild` | 0:すべて, 1:子課題以外, 2:子課題, 4:親課題 |

## 共通パターン

### ページネーション
- `count`（1-100）と `offset` を組み合わせる

### 複数値パラメータ
```
statusId[]=1&statusId[]=2
```

### ID指定方式
- `{issueIdOrKey}`: 数値ID または課題キー（例: `PROJECT-123`）
- `{projectIdOrKey}`: 数値ID またはプロジェクトキー（例: `PROJECT`）

## Pythonスクリプト

基本的な操作は [scripts/backlog_api.py](./scripts/backlog_api.py) を実行または参照する。

```python
# 実行例
python scripts/backlog_api.py --space myspace --api-key YOUR_KEY get-issues --project-id 12345
```
