# JaAddress

日本の住所文字列を、都道府県・市区町村・町字・丁目・番地に分けて正規化する .NET ライブラリです。
[geolonia/normalize-japanese-addresses](https://github.com/geolonia/normalize-japanese-addresses) の C# 移植版です。

- 対象: .NET 8 以降
- 政令指定都市の区、郡に属する町村の正規化
- 郡名・区名・市名の省略補正、「大字」の省略入力の照合
- 全角数字・ハイフンの半角変換、異体字（旧字体）の畳み込み
- 前後に別の文字列がある場合の住所の抽出（BestEffort）

## 住所データについて

このパッケージには住所データが含まれていません。
dotnet tool の [JaAddress.DataBuilder](https://www.nuget.org/packages/JaAddress.DataBuilder) でデータを取得してください。

```bash
dotnet tool install -g JaAddress.DataBuilder
jaaddress-databuilder --output ./data             # 全都道府県
jaaddress-databuilder --output ./data --pref 東京都  # 特定の都道府県のみ
```

## 使い方

```bash
dotnet add package JaAddress
```

```csharp
using JaAddress.Core;
using JaAddress.Core.Options;
using JaAddress.Core.Services;

// DI に登録する（DataDirectory には DataBuilder の出力先を指定する）
builder.Services.AddJaAddress(new JaAddressOptions {
    DataDirectory = "./data",
});

// IAddressService を受け取って解析する
public class MyService(IAddressService addressService) {
    public async Task ExampleAsync() {
        var result = await addressService.ParseAsync(
            "東京都新宿区西新宿1丁目2-3",
            new AddressParseOptions { SplitRemainder = true });

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

オプションの一覧や Web API サーバーについては、[リポジトリの README](https://github.com/k14a/JaAddress#readme) を参照してください。

## ライセンス

ライブラリは [MIT License](https://github.com/k14a/JaAddress/blob/main/LICENSE) で公開しています。

DataBuilder で取得する住所データは、「アドレス・ベース・レジストリ」（デジタル庁）をもとに
株式会社 Geolonia が作成した「Geolonia 住所データ v2」です。
ライセンスは [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/deed.ja)（&copy; 2024 Geolonia Inc.）で、
データを使うアプリケーションでは出典の表示が必要です。
