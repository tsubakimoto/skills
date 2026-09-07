# Git worktree 学習ガイド

## 1. worktree とは

通常、Git リポジトリでは1つのディレクトリで1つのブランチを checkout します。
`git worktree` を使うと、同じリポジトリの管理情報を共有しながら、複数のディレクトリで
異なるブランチを同時に checkout できます。

例えば、`main` で通常作業を続けながら、隣のディレクトリで `feature/login` を実装できます。
コミット履歴やオブジェクトは共有されるため、リポジトリを丸ごと複製するより効率的です。

```text
inventory/                 main の worktree
inventory-login/           feature/login の worktree
inventory-hotfix/          hotfix/123 の worktree
```

## 2. ブランチとの違い

ブランチと worktree は競合する機能ではなく、役割が異なります。

- **ブランチ**はコミット履歴上の作業系列です。`feature/login` のように、どの変更を積み重ねるかを表します。
- **worktree**はブランチを実際に checkout して作業するディレクトリです。ソースコード、未コミット変更、ビルド成果物などを置きます。

通常の Git 操作では、1つの worktree で `git switch` によりブランチを切り替えます。この方法では、
別のブランチの作業に移るたびに現在の未コミット変更をコミット・stash・破棄のいずれかで整理する必要があります。

```text
通常の1 worktree
inventory/  main → git switch feature/login → feature/login

複数 worktree
inventory/        main
inventory-login/  feature/login
```

worktree はブランチを増やす機能ではありません。先にブランチを作成して既存ブランチを checkout することも、
`git worktree add -b` でブランチ作成と worktree 作成を一度に行うこともできます。

同じ通常ブランチを複数の worktree で同時に checkout できない点は重要です。2つのディレクトリで同じブランチを
編集すると、未コミット変更の所在が分かりにくくなり、操作ミスにつながるためです。Git がこの制約を設けています。
特定コミットを読むだけなら、ブランチを共有せずに detached worktree を使います。

```bash
git worktree add --detach ../inventory-review <commit-ish>
```

## 3. 最初に確認するコマンド

作業を始める前に、現在の場所、ブランチ、変更、登録済み worktree を確認します。

```bash
git rev-parse --show-toplevel
git branch --show-current
git status --short
git worktree list --verbose
```

スクリプトで解析する場合は、安定した機械可読形式を使います。

```bash
git worktree list --porcelain
```

## 4. 新しい作業を始める

### 新しいブランチを作る

```bash
git worktree add -b feature/login ../inventory-login origin/main
```

これは `origin/main` を起点に `feature/login` を作り、`../inventory-login` に checkout します。
既存の `main` worktree は切り替わりません。

`origin/main` が最新である必要がある場合だけ、先に fetch します。

```bash
git fetch origin main
git worktree add -b feature/login ../inventory-login origin/main
```

### 既存ブランチを checkout する

```bash
git worktree add ../inventory-login feature/login
```

同じブランチが別の worktree で checkout 済みだと、Git は通常この操作を拒否します。
これは同じブランチを2つの通常 worktree で同時に変更する事故を防ぐためです。

### 一時的にコミットを確認する

ブランチを作らず、特定のコミットを確認したい場合は detached worktree を使います。

```bash
git worktree add --detach ../inventory-review <commit-ish>
```

## 5. worktree を使った並列開発

worktree は、複数の作業を**同時に保持**したいときに有効です。ブランチを切り替える代わりに、
タスクごとの worktree へ移動します。例えば、機能開発を続けながら緊急バグ修正や pull request の
レビューを始められます。

```text
inventory/                 main: CI の確認・統合・通常作業
inventory-feature-search/  feature/search: 機能開発
inventory-hotfix-482/      hotfix/482: 緊急修正
inventory-pr-917/          detached HEAD: pull request のレビュー
```

### 個人での並列作業

1. `main` の worktree は統合確認用として安定した状態に保ちます。
2. タスクごとに専用ブランチと専用 worktree を作ります。
3. エディター、ターミナル、テストプロセスは worktree ごとに開きます。
4. タスクが終わったら変更をコミットまたは退避し、worktree を削除します。

```bash
# feature/search の作業を開始
git worktree add -b feature/search ../inventory-feature-search origin/main

# 緊急修正を別ディレクトリで開始
 git worktree add -b hotfix/482 ../inventory-hotfix-482 origin/main
```

各 worktree の未コミット変更と index は独立します。一方で Git オブジェクト、ローカルブランチ、
リモート設定は共有されます。そのため、一方の worktree で作ったコミットは、他方の worktree からも
すぐに参照できます。

### チームでの並列開発

チームメンバーごとに clone を用意する点は通常の Git 運用と変わりません。worktree は、**同じ開発者が
複数タスクを切り替えずに進める**ためのローカルな仕組みです。各タスクを別ブランチとして push し、
pull request と CI で統合する流れは従来どおりです。

並列開発で衝突を減らすには、次を意識します。

- worktree 名とブランチ名をタスクに対応させる
- 各 worktree で依存関係や生成物が必要かを個別に確認する
- 共有されない設定ファイルや環境変数を worktree ごとに管理する
- 同じファイルを複数ブランチで大きく変更する作業は、早めにチームと調整する
- worktree の削除前に、対象ブランチのコミット・push・pull request 状態を確認する

### 並列開発での注意

worktree が分離するのは作業ディレクトリと未コミット状態です。ポート番号、Docker コンテナ名、
データベース、キャッシュディレクトリ、外部サービスのテストデータなどは自動では分離されません。
同時起動するアプリケーションでは、worktree ごとにポートや環境変数を変える必要がある場合があります。

また、リポジトリ外に置く依存関係キャッシュやビルド出力は共有されることがあります。テスト結果が
予想と異なる場合は、実行場所と設定ファイルを確認してください。

## 6. worktree の状態を管理する

```bash
# 詳細一覧
git worktree list --verbose

# 長期間使う・外付け媒体に置く worktree をロック
git worktree lock --reason "外付け SSD 上で使用中" <path>

# ロック解除
git worktree unlock <path>

# 登録済み worktree を別の場所へ移動
git worktree move <old-path> <new-path>
```

ロックは、worktree の場所が一時的に利用できない場合などに、Git の prune から保護するために使います。

## 7. 作業完了後に削除する

まず変更を確認します。

```bash
git -C ../inventory-login status --short
```

出力が空なら、通常の削除を実行できます。

```bash
git worktree remove ../inventory-login
git worktree list --verbose
```

変更が残っている場合は、次のいずれかを選びます。

- 必要な変更をコミットしてから削除する
- パッチや stash などで退避してから削除する
- 変更を残したまま作業を継続する
- 変更を破棄することを明示的に決めた場合だけ force を検討する

`git worktree remove --force` は未コミット変更を失う可能性があります。エラーを解消するためだけに
無条件で使わないでください。

## 8. 手動で移動・削除してしまった場合

### worktree のフォルダを移動した

フォルダが新しい場所に存在するなら、登録情報を修復します。

```bash
git worktree repair <new-path>
git worktree list --verbose
```

### worktree のフォルダを削除した

まず、何が prune 対象になるかを確認します。

```bash
git worktree prune --dry-run --verbose
```

表示内容を確認して問題がなければ、登録情報を整理します。

```bash
git worktree prune --verbose
git worktree list --verbose
```

`prune` は stale な登録情報を削除する操作であり、既に存在しないフォルダを復元するものではありません。
まだ必要な worktree なら、prune する前にフォルダやバックアップを復元してください。

## 9. よくあるエラー

### `branch is already checked out`

対象ブランチがどの worktree で使われているか確認します。

```bash
git worktree list --verbose
```

安全な対応は、別のブランチを使う、既存 worktree で作業する、または既存 worktree をクリーンにして
正式な手順で削除することです。`--force` で同じブランチを重複 checkout するのを既定の解決策にしません。

### `worktree is locked`

ロック理由を確認し、意図的な保護でないことを確認してから解除します。

```bash
git worktree list --verbose
git worktree unlock <path>
```

### パスに空白がある

パス全体を引用符で囲みます。

```bash
git worktree add -b feature/docs "../inventory docs" origin/main
```

## 10. 覚えておくこと

- worktree ごとに checkout されるブランチは異なる
- 通常、同じブランチを複数 worktree で checkout できない
- `list --porcelain` は機械的な確認に向く
- `remove` の前に対象 worktree の status を確認する
- stale 登録の掃除は `prune --dry-run` でプレビューしてから行う
- 実在する移動済み worktree の修復は `repair` を使う
- `--force` は安全確認の代わりにならない

## 参考

- [Git 公式ドキュメント: git-worktree](https://git-scm.com/docs/git-worktree)
- [Git 公式ドキュメント: git-worktree add](https://git-scm.com/docs/git-worktree#Documentation/git-worktree.txt-add)
