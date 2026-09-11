---
name: summarized-post
description: Retrieve the content from the specified URL and create a series of posts to share the article on X (Twitter).
---

# Overview
指定されたURLの内容をフェッチし、記事をX (Twitter) で紹介する連続ポストを作成する。

## Steps
1. 記事の要点を抽出する。
2. 1要点1ポストとする。
3. 記事内容をもとにポスト数を決め、ユーザーに同意を得る。最大は10ポスト。
4. 最初のポストは `assets/top-post-template.txt` を使用する。
    - `{post title}`: 記事のタイトル
    - `{post url}`: 記事のURL
5. 続くポストのテンプレートは `assets/summarized-post-template.txt` を使用する。
    - `{current}`: 現在のポスト番号
    - `{total}`: ポスト総数
    - `{post title}`: この要点のタイトル
    - `{post body}`: この要点の内容
6. 各ポストは、テンプレートの固定文・見出し・本文を含めて、全角・半角の混在を考慮した文字幅で120〜140文字程度（目安130文字）に収める。130文字を大きく超えないよう、本文を作成後に文字数を確認して調整する。
7. ポストを作成したら出力する。