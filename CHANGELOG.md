# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- 異体字（旧字体・許容字体）の畳み込み機能（`JaAddressOptions.FoldItaiji`、既定 false のオプトイン）。
  有効時、入力住所文字列と住所辞書の名称の双方に変換マップを適用してから照合するため、
  「須惠町」(惠) と「須恵町」(恵) のような字体の揺れを吸収できる（NFKC では畳み込まれない）。
- 既定の異体字マップを埋め込みリソース `Resources/itaiji.tsv` として同梱。
  `JaAddressOptions.ItaijiMapPath`（外部 TSV）、データディレクトリ直下の `itaiji.local.tsv`、
  `JaAddressOptions.AdditionalItaiji`（コード）で利用者が追加・上書き可能。
- `IItaijiFolder` を DI で公開（呼び出し側で同じマップを再利用可能）。
- `AddressParseOptions.FoldItaiji`（`bool?`）でリクエスト単位の無効化に対応。
- API：`JaAddress:FoldItaiji` / `JaAddress:ItaijiMapPath` 設定、各 `/parse` エンドポイントの
  `foldItaiji` パラメータ、`POST /parse` の `foldItaiji` フィールドを追加。
- `JaAddress.Integration.Tests`：リポジトリ直下の `data/`（DataBuilder 生成の本番相当データ）を
  そのまま読み込む結合テストを追加。`data/` が無い環境ではスキップされる。
- `AddressParseResult.Koaza`：`SplitRemainder=true` で町字のあとに小字があれば読み取り、その後ろを丁目・番地として分割する
  （例：`保原町大泉字大地内95-5` → Town「保原町大泉」、Koaza「字大地内」、Block「95-5」。従来は小字と番地を Remainder に残していた）。
  辞書の小字と「字」の有無を問わず照合する。API の `/parse` 応答と TSV 出力に `koaza` を追加。

### Fixed

- 慣習として町村名の前に島名を書いた住所（`東京都八丈島八丈町三根`・`東京都三宅島三宅村坪田`）の市区町村が取れない問題を修正。
  辞書（アドレス・ベース・レジストリ）には島名がないため、島名を読み飛ばす。町村名がなく島名だけの場合（`東京都八丈島三根`）は
  島名を町村名として読み、`Corrected` を true にする。対象は町村名が島名を含まない八丈島（八丈町）・三宅島（三宅村）。
- 町字名・小字名の漢数字・全角数字を算用数字で書いた入力が一致しない問題を修正（`SplitRemainder=true`）。
  辞書の名前の数字を算用数字にした表記とも照合する（例：`美園2条1丁目2` → Town「美園二条」、`北1条西2丁目3` → Town「北一条西」、
  `古町通5番町` → Town「古町通五番町」。従来は町字以下が空で Remainder に残っていた）。名前の末尾の数字は変換しない。
  "N丁" で終わる町字（`蔵前町二丁`）が `蔵前町2丁目` の "丁目" の途中まで一致する場合は採用しない。
- 数字を含む辞書の小字（札幌の `十一丁目北`、岩手の `１４地割` 等）を丁目・番地より優先して読む
  （`平和通11丁目北6-19` を Street「11丁目」＋番地なし、`村崎野14地割453-5` を Block「14」と読んでいた）。
  直後に丁目・番地が続くか住所がそこで終わる場合に限る。
- 地番の冠称（甲・乙・…・癸）の直後に数字が続く場合、冠称を Koaza として読み番地を取る（`来迎寺甲2621-4` → Koaza「甲」、Block「2621-4」）。
- 小字のあとの番地を丁目省略形・漢字丁目形として読まない（`北崎町井田1-1` を Street「1丁目」・Block「1」と読んでいた。小字のある地域に丁目はない）。

### Security

- `Microsoft.OpenApi` を推移的依存の 2.4.1 から 2.7.5 へ明示的に引き上げ（`JaAddress.Api`）。
  循環スキーマ参照によるスタックオーバーフローの脆弱性 [GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc)（高）に対応。

## [0.1.1] - 2026-07-08

### Added

- DataBuilder 用 `Dockerfile` を追加（.NET SDK なしで Docker から住所データ取得が可能に）
- `JaAddress.Api` 用 `Dockerfile.api` を追加（API のコンテナ実行が可能に）
- API の全エンドポイント（`GET /parse`・`POST /parse`・`POST /parse/tsv`）に `split_remainder` パラメータを追加
- `NormalizeNumber` で長音符（U+30FC）・マイナス記号（U+2212）をハイフンとして正規化

### Fixed

- `SplitRemainder=true` 時に番地/番（"7番地"・"1番"）が丁目として誤判定される問題を修正し、`Block` として正規化するよう変更
- 市区町村逆引き補正で1文字の町字が誤マッチする問題を修正

### Changed

- 番号形式の番地（"1番20号"・"7番地5"）を "1-20"・"7-5" に正規化（`SplitRemainder=true`）
- 漢字丁目形（"一丁目"）を数字丁目形（"1丁目"）に正規化（`SplitRemainder=true`）
- `AddressService` の `ja.json` 読み込みを `LoadJaRootAsync` に集約してキャッシュ化し、`GetPrefecturesAsync`・`GetCitiesAsync`・`AggregateSubCityTownsAsync` 間で共有
- `GetCitiesAsync` の結果を `ConcurrentDictionary` でキャッシュ化（`ParseAsync` 大量呼び出し時のパフォーマンス改善）

## [0.1.0] - 2026-06-03

### Added

- 都道府県・市区町村・町字・丁目・番地への住所分解（`AddressService.ParseAsync`）
- 政令指定都市の区、郡に属する町村の正規化
- 郡名・区名・市名の省略補正（`Corrected` フラグで通知）
- 「大字」省略入力の正規化（`NormalizeOaza`、デフォルト有効）
- 全角数字・ハイフン類の半角変換（`NormalizeNumber`、デフォルト有効）
- 前後テキストを含む文字列からの住所抽出（`BestEffort` モード、`Offset` で開始位置を返す）
- 番地以降の建物名を `Remainder` に分離
- 番地と建物名の間の半角・全角スペースの自動トリム
- ASP.NET Core Minimal API（`JaAddress.Api`）
  - `GET /parse` — 住所を1件パース
  - `POST /parse` — 住所を複数件パース（JSON、最大20件）
  - `POST /parse/tsv` — TSV ファイルで住所を一括パース（最大10,000件）
  - `GET /parse/tsv/template` — TSV テンプレートのダウンロード
  - `GET /prefectures` — 都道府県一覧
  - `GET /prefectures/{pref}/cities` — 市区町村一覧
  - `GET /prefectures/{pref}/cities/{city}/towns` — 町字一覧
- Geolonia 住所データ取得 CLI（`JaAddress.DataBuilder`）
- GitHub Actions による CI（ビルド・テスト自動実行）

[0.1.1]: https://github.com/k14a/JaAddress/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/k14a/JaAddress/releases/tag/v0.1.0
