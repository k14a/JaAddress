# JaAddress

日本の住所を正規化・分解する .NET 10 ライブラリ。

[geolonia/normalize-japanese-addresses](https://github.com/geolonia/normalize-japanese-addresses) の C# 移植版。

## 機能

- 都道府県・市区町村・町字・丁目・番地への分解
- 政令指定都市の区、郡に属する町村の正規化
- 郡名・区名・市名の省略補正
- 「大字」省略入力の正規化（オプション）
- 全角数字・ハイフンの半角変換（デフォルト有効）
- 前後テキストを含む文字列からの住所抽出（BestEffort モード）

---

## はじめに

### 1. データの準備

`JaAddress.DataBuilder` で住所データを取得します。

#### Docker を使う場合（.NET SDK 不要）

```bash
# イメージをビルド（初回のみ）
docker build -t jaaddress-databuilder .

# 全都道府県を取得してカレントディレクトリの data/ に保存
docker run --rm -v "$(pwd)/data:/data" jaaddress-databuilder

# 特定の都道府県のみ
docker run --rm -v "$(pwd)/data:/data" jaaddress-databuilder --pref 東京都
```

#### .NET SDK を使う場合

```bash
# 全都道府県（初回は時間がかかります）
dotnet run --project src/JaAddress.DataBuilder -- --output ./data

# 特定の都道府県のみ
dotnet run --project src/JaAddress.DataBuilder -- --output ./data --pref 東京都

# ヘルプ
dotnet run --project src/JaAddress.DataBuilder -- --help
```

出力先は `--output` で指定したディレクトリ（上記例では `./data`）です。

### 2. DI への登録

```csharp
using JaAddress.Core;
using JaAddress.Core.Options;

builder.Services.AddJaAddress(new JaAddressOptions {
    DataDirectory = "./data"   // DataBuilder の出力先パス
});
```

### 3. 利用

```csharp
using JaAddress.Core.Services;

public class MyService(IAddressService addressService) {

    public async Task ExampleAsync() {
        // 住所を分解する（町字・番地まで）
        var options = new AddressParseOptions { SplitRemainder = true };
        var result = await addressService.ParseAsync(
            "東京都新宿区西新宿1丁目2-3", options);

        if (result is not null) {
            Console.WriteLine(result.Prefecture.Name);  // 東京都
            Console.WriteLine(result.City.Name);        // 新宿区
            Console.WriteLine(result.Town?.Name);       // 西新宿
            Console.WriteLine(result.Street);           // 1丁目
            Console.WriteLine(result.Block);            // 2-3
        }
    }
}
```

---

## API リファレンス

### `IAddressService`

| メソッド | 説明 |
|---------|------|
| `GetPrefecturesAsync()` | 都道府県一覧を返す |
| `GetCitiesAsync(prefName)` | 指定都道府県の市区町村一覧を返す |
| `GetTownsAsync(prefName, cityName)` | 指定市区町村の町字一覧を返す。政令市名（`さいたま市`）や郡名（`泉南郡`）を指定すると配下全体を集約して返す |
| `ParseAsync(address, options?)` | 住所文字列を分解する。特定できなかった場合は `null` |

### `AddressParseOptions`

| プロパティ | 型 | デフォルト | 説明 |
|-----------|-----|-----------|------|
| `SplitRemainder` | `bool` | `false` | `true` にすると町字・丁目・番地まで分解する |
| `NormalizeNumber` | `bool` | `true` | 全角数字・ハイフン類を半角に変換してから解析する |
| `BestEffort` | `bool` | `false` | 入力の先頭以外に都道府県名が見つかった場合も解析を試みる。`Offset` で開始位置を返す |
| `NormalizeOaza` | `bool` | `true` | 「大字」省略入力（例: `久保`）をデータ上の正規形（例: `大字久保`）に照合する |
| `FoldItaiji` | `bool?` | `null` | このリクエストで異体字（旧字体）畳み込みを行うか。`null` はサーバ設定（`JaAddressOptions.FoldItaiji`）に従う。`false` で明示的に無効化できる |

### 異体字（旧字体）の畳み込み

`JaAddressOptions.FoldItaiji = true`（既定 `false` のオプトイン）にすると、`惠→恵`・`舘→館` などの
異体字を正規形へ畳み込んでから住所照合を行う。入力文字列と住所辞書の名称の双方に同じマップを適用するため、
「須惠町」(惠) と「須恵町」(恵) の揺れを吸収できる（NFKC 正規化では畳み込まれない）。
出力の `City.Name` / `Town.Name` は辞書の元表記（正式表記）を保持する。

| `JaAddressOptions` プロパティ | 説明 |
|---|---|
| `FoldItaiji` | 畳み込みを有効にする |
| `ItaijiMapPath` | 追加の異体字マップ TSV（`変換元<TAB>変換先`、1 文字 → 1 文字）。既定の埋め込みマップにマージ |
| `AdditionalItaiji` | コードから追加する `IReadOnlyDictionary<char,char>` |

データディレクトリ直下に `itaiji.local.tsv` を置いてもマージされる（優先度: 既定 < `ItaijiMapPath` < `itaiji.local.tsv` < `AdditionalItaiji`）。

### `AddressParseResult`

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `Prefecture` | `Prefecture` | 都道府県 |
| `City` | `City` | 市区町村（`Name` は完全名: `泉南郡熊取町`、`大阪市北区` など） |
| `Town` | `Town?` | 町字（`SplitRemainder=true` 時のみセット） |
| `Street` | `string?` | 丁目（例: `1丁目`） |
| `Block` | `string?` | 番地（例: `2-3`） |
| `Remainder` | `string` | パースできなかった残余文字列 |
| `Corrected` | `bool` | 郡名・市名・区名の省略補正が行われた場合 `true` |
| `Offset` | `int` | `BestEffort=true` 時、元の入力中で住所が始まった文字位置 |

### `City`

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `Name` | `string` | 完全な市区町村名（例: `大阪市北区`、`泉南郡熊取町`） |
| `County` | `string?` | 郡名（郡に属する場合のみ、例: `泉南郡`） |
| `Ward` | `string?` | 区名（政令指定都市の場合のみ、例: `北区`） |

---

## 使用例

### 郡・区を含む住所

```csharp
// 郡に属する町村
var result = await addressService.ParseAsync(
    "大阪府泉南郡熊取町大字久保2983-1",
    new AddressParseOptions { SplitRemainder = true });

// result.City.Name    → "泉南郡熊取町"
// result.City.County  → "泉南郡"
// result.Town?.Name   → "大字久保"
// result.Block        → "2983-1"
```

### 郡名省略の補正

```csharp
var result = await addressService.ParseAsync("奈良県吉野町大字吉野山");

// result.City.Name  → "吉野郡吉野町"（補完された）
// result.Corrected  → true
```

### 「大字」省略の正規化

```csharp
var options = new AddressParseOptions {
    SplitRemainder = true,
    NormalizeOaza = true
};
var result = await addressService.ParseAsync(
    "大阪府泉南郡熊取町久保2983-1", options);

// result.Town?.Name → "大字久保"（正規形に補完された）
// result.Block      → "2983-1"
```

### BestEffort（住所部分の抽出）

```csharp
var options = new AddressParseOptions { BestEffort = true };
var result = await addressService.ParseAsync("勤務先：東京都新宿区西新宿", options);

// result.Prefecture.Name → "東京都"
// result.Offset          → 4（"勤務先：" の文字数）
```

---

## Web API サーバー

`JaAddress.Api` を使うとライブラリを REST API として利用できます。
詳しくは [src/JaAddress.Api/README.md](src/JaAddress.Api/README.md) を参照してください。

```bash
dotnet run --project src/JaAddress.Api
# → http://localhost:5000/swagger で Swagger UI を確認
```

---

## プロジェクト構成

| プロジェクト | 種別 | 説明 |
|------------|------|------|
| `JaAddress.Core` | classlib | 実装の中心。`IAddressService`・モデル・DI 登録 |
| `JaAddress.Api` | webapi | ASP.NET Core Minimal API |
| `JaAddress.DataBuilder` | console | Geolonia API からデータを取得する CLI |

---

## ライセンス

本ソフトウェアは [MIT License](LICENSE) の下で公開されています。

---

## 出典・データライセンス

住所データは「アドレス・ベース・レジストリ」（デジタル庁）をもとに
株式会社 Geolonia が作成した「Geolonia 住所データ v2」を使用しています。

ライセンス: [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/deed.ja)

&copy; 2024 Geolonia Inc.
