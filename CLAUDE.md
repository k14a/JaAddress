# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

JaAddress は日本の住所を正規化する .NET 10 ライブラリ。[geolonia/normalize-japanese-addresses](https://github.com/geolonia/normalize-japanese-addresses) の C# 移植版。

住所データは Geolonia 住所データ v2（アドレス・ベース・レジストリ由来、CC BY 4.0）を使用。

## Commands

```bash
# ビルド
dotnet build

# 全テスト実行
dotnet test

# 特定プロジェクトのテストのみ
dotnet test tests/JaAddress.Core.Tests/

# 単一テスト実行
dotnet test --filter "FullyQualifiedName~ParseAsync_SplitRemainder"

# API 起動（data/ ディレクトリが必要）
dotnet run --project src/JaAddress.Api

# データ取得（全都道府県）
dotnet run --project src/JaAddress.DataBuilder -- --output ./data

# データ取得（特定都道府県）
dotnet run --project src/JaAddress.DataBuilder -- --output ./data --pref 東京都
```

## Architecture

### プロジェクト構成

- **`JaAddress.Core`** — 実装の中心。`IAddressService` / `AddressService`、モデル、`AddressParseOptions`、`AddressParseResult`。`AddJaAddress()` 拡張メソッドでDI登録。
- **`JaAddress.Api`** — ASP.NET Core Minimal API。`/parse`（GET/POST/TSV）と `/prefectures` エンドポイント。Swagger UI あり。
- **`JaAddress.DataBuilder`** — Geolonia API からデータを取得してローカル JSON として保存する CLI（`System.CommandLine`）。
- **`JaAddress.Domain` / `JaAddress.Application` / `JaAddress.Infrastructure`** — 現時点ではスタブ（`Class1.cs` のみ）。将来の拡張用。

### データフロー

1. `DataBuilder` が `https://japanese-addresses-v2.geoloniamaps.com/api` から JSON を取得し、以下の構造で保存:
   - `data/ja.json` — 都道府県・市区町村一覧
   - `data/ja/{都道府県名}/{市区町村名}.json` — 町字データ
2. `AddressService` はこれらのファイルを遅延読み込みし、メモリキャッシュ（都道府県: フィールド変数、町字: `ConcurrentDictionary`）で保持する。

### 住所パース手順（`AddressService.ParseAsync`）

1. 入力文字列が都道府県名で始まるか照合（完全前方一致）
2. 残りが市区町村名で始まるか照合（長い名前を優先して誤検知防止）
3. `SplitRemainder=true` の場合、さらに町字・Street（`1丁目`等）・Block（`2-3`等）に分割
4. `NormalizeNumber=true`（デフォルト）の場合、全角数字・ハイフン類を事前に半角変換

### 町字 JSON のフォーマット

本番データ（DataBuilder が生成）の `{市区町村}.json` は `oaza_cho` フィールドを使用。`AddressService` も `oaza_cho` を読む。テスト用フィクスチャ（`AddressServiceFixture`）は旧フォーマット（`town`/`lat`/`lng`）でデータを生成しているため、テスト時には `oaza_cho` の代わりに `town` フィールドが使われている点に注意（現在のテストは通過しているが、フィクスチャと本番データの形式が異なる）。

### テスト

xUnit + `IClassFixture` パターン。`AddressServiceFixture` がテンポラリディレクトリに最小限の JSON を生成してDIコンテナを構築する。実テストは `JaAddress.Core.Tests` にのみ存在し、他のテストプロジェクトはスタブ。
