# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
