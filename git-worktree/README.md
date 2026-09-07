# git-worktree スキル

Git worktree を使って、1つのリポジトリで複数ブランチを同時に扱うためのスキルです。

## できること

- 新しいブランチ用 worktree の作成
- 既存 worktree とブランチの一覧確認
- worktree の移動・ロック・アンロック
- 作業完了後の安全な削除
- 手動移動後のメタデータ修復
- 手動削除後に残った stale 登録の prune
- 「このブランチは別の worktree で使用中」などのエラーの切り分け

## 安全方針

状態を変更する前に、対象リポジトリと worktree の状態を確認します。未コミット変更を
確認せずに削除したり、エラーを `--force` で無条件に回避したりしません。

通常の回答は次の順序で構成します。

1. **状況** — 現在の worktree・ブランチ・目的
2. **安全なコマンド** — 実行するコマンド
3. **実行前の注意** — 変更、パス、ブランチ、破壊的操作の注意
4. **確認** — 実行後に結果を確認するコマンド

## 使い方の例

```text
main を触らずに issue 123 の作業を始めたい
```

```text
不要な worktree を変更を失わずに削除したい
```

```text
手動で移動した worktree が prunable になった
```

## 参考資料

- [SKILL.md](./SKILL.md)
- [git-worktree 学習ガイド](./git-worktree.md)
- [Git 公式ドキュメント: git-worktree](https://git-scm.com/docs/git-worktree)
