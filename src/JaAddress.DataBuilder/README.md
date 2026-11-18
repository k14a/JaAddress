# JaAddress.DataBuilder

住所の正規化ライブラリ [JaAddress](https://www.nuget.org/packages/JaAddress) が使う住所データ
（Geolonia 住所データ v2）を取得して、ローカルに保存する dotnet tool です。

- 対象: .NET 8 以降のランタイム

## インストール

```bash
# グローバルにインストールする
dotnet tool install -g JaAddress.DataBuilder

# リポジトリごとに版を固定する場合（.config/dotnet-tools.json に記録される）
dotnet new tool-manifest
dotnet tool install JaAddress.DataBuilder
```

.NET 10 以降の SDK なら、インストールせずに `dnx JaAddress.DataBuilder --pref 東京都` のように実行することもできます。

## 使い方

```bash
# 全都道府県（初回は時間がかかります）
jaaddress-databuilder --output ./data

# 特定の都道府県のみ
jaaddress-databuilder --output ./data --pref 東京都

# 1 つのアーカイブ（.tar.gz / .tgz / .zip）にまとめて出力する
jaaddress-databuilder --archive ./ja-address-data.tar.gz

# ヘルプ
jaaddress-databuilder --help
```

`--output` を省略すると、カレントディレクトリの `data` に出力します。
出力したディレクトリを、JaAddress の `JaAddressOptions.DataDirectory` に指定してください。

詳しくは [リポジトリの README](https://github.com/k14a/JaAddress#readme) を参照してください。

## ライセンス

ツールは [MIT License](https://github.com/k14a/JaAddress/blob/main/LICENSE) で公開しています。

取得する住所データは、「アドレス・ベース・レジストリ」（デジタル庁）をもとに
株式会社 Geolonia が作成した「Geolonia 住所データ v2」です。
ライセンスは [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/deed.ja)（&copy; 2024 Geolonia Inc.）で、
データを使うアプリケーションでは出典の表示が必要です。
