---
name: azure-paas-support
description: >
  Use this skill whenever the user asks a question, reports an error, or needs troubleshooting
  guidance for Azure PaaS services covered by the Japan PaaS Support Team Blog, including Azure
  App Service, Azure Functions, Storage, Azure AI Search, API Management, Container Apps,
  Communication Services, Service Bus, Event Hubs, or related networking, authentication,
  certificates, runtimes, triggers, bindings, logging, and deployment behavior. Trigger for
  Japanese or English Azure support questions even when the user does not mention the blog.
  Search azure.github.io/jpazpaas at request time, read the relevant article bodies, and answer
  in Japanese with evidence, applicability conditions, concrete checks, remediation steps, and
  source URLs. Do not use for infrastructure deployment or for general Azure architecture advice
  that is unrelated to troubleshooting or support knowledge.
license: Proprietary. LICENSE has complete terms.
---

# Azure PaaS Support

Japan PaaS Support Team Blog を質問時点で検索し、該当記事の本文を根拠に Azure PaaS の質問へ回答する。

## Workflow

1. 質問から次の検索要素を抽出する。
   - Azure サービスと機能
   - エラーコードまたは完全なエラーメッセージ
   - 症状と期待する動作
   - OS、ランタイム、SKU、認証方式、ネットワーク構成
2. `site:azure.github.io/jpazpaas` を付けた検索クエリを2〜4個作る。最初は最も識別力の高いエラー文や製品用語を使い、結果が弱ければ同義語や上位概念へ広げる。
3. Web検索を実行し、候補記事を選ぶ。検索結果のスニペットだけで回答しない。
4. 候補記事を開き、本文で症状、原因、前提、対処、公開・更新時点を確認する。通常は上位1〜3記事を読む。
5. 質問への適用可否を判断する。対象OS、ホスティングモデル、ランタイム、SKU、ネットワーク経路などが違う場合は、その差を明示する。
6. 根拠が十分なら回答テンプレートに沿って答える。複数記事を使う場合は、どの記述がどの記事に基づくか分かるようにする。
7. 関連記事を確認できない場合は、サイト内では根拠を確認できなかったと明示し、Microsoft Learn での追加調査を提案する。ブログ外の知識で結論を埋めない。

詳細な検索方法と関連性の判断は [references/search-guidance.md](./references/search-guidance.md) を参照する。

## Search rules

- 検索ドメインは `azure.github.io/jpazpaas` に限定する。
- エラー文は引用符で囲んだ完全一致検索も試す。
- 日本語名と英語の正式製品名を使い分ける。
- 1件目が部分一致にすぎない場合は、別クエリと別記事で確認する。
- URLの日付だけで鮮度を判断せず、本文やメタデータの公開・更新時点を確認する。
- 記事中の Microsoft Learn や公式アナウンスへのリンクは補助参照として示せるが、サイト外の内容を読んで補完するのはユーザーが追加調査を求めた場合に限る。

## Answer rules

- まず質問へ直接答え、検索過程の実況から始めない。
- 記事に書かれた事実と、質問への適用判断を区別する。
- 記事にない原因、手順、コマンド、期限を推測しない。
- 手順は実行順にし、確認対象となる設定名やポータル項目を具体的に示す。
- 破壊的変更、期限、セキュリティ、サービス停止の可能性は目立つように示す。
- 時点依存の情報には「記事公開・更新時点」を添える。
- 参考記事はタイトルと完全なURLを記載する。

## Output format

原則として [assets/answer-template.md](./assets/answer-template.md) を使う。単純な質問では不要な見出しを省略してよいが、結論と参考記事は残す。

## No matching article

十分に関連する本文が見つからない場合は、次の3点を簡潔に伝える。

1. 使用した主要な検索観点
2. Japan PaaS Support Team Blog 内では根拠となる記事を確認できなかったこと
3. Microsoft Learn の現行ドキュメントで追加調査する提案

検索結果がないのに、一般知識だけでブログ由来の回答に見せかけてはいけない。

