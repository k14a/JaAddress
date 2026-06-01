# JaAddress

日本の住所を正規化する .NET 10 ライブラリ。

[geolonia/normalize-japanese-addresses](https://github.com/geolonia/normalize-japanese-addresses) の C# 移植版。

## 出典・ライセンス

住所データは「アドレス・ベース・レジストリ」（デジタル庁）をもとに
株式会社 Geolonia が作成した「Geolonia 住所データ v2」を使用しています。

ライセンス: [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/deed.ja)

## プロジェクト構成

| プロジェクト | 種別 | 説明 |
|---|---|---|
| `JaAddress.Domain` | classlib | インターフェース・エンティティ・値オブジェクト |
| `JaAddress.Application` | classlib | 正規化ロジック本体 |
| `JaAddress.Infrastructure` | classlib | データソース実装・キャッシュ |
| `JaAddress.Api` | webapi | ASP.NET Core Web API |
| `JaAddress.DataBuilder` | console | データ取得・変換 CLI |

## DataBuilder の使い方

```bash
# 全都道府県のデータを取得（data/ ディレクトリに保存）
dotnet run --project src/JaAddress.DataBuilder -- --output ./data

# 東京都のみ取得
dotnet run --project src/JaAddress.DataBuilder -- --output ./data --pref 東京都

# デバッグログ付き
dotnet run --project src/JaAddress.DataBuilder -- --output ./data --verbose

# ヘルプ
dotnet run --project src/JaAddress.DataBuilder -- --help
```
