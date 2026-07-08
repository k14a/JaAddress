# TODO: AddressService キャッシュ改善

## 背景

呼び出し側（RivalDataCsFileCheck.IndeedPlus）で 172 万件規模の CSV を処理した際、
WorkLocation 住所正規化ありで **5224 秒**かかった（WorkLocation なし 195 万件では 1671 秒）。

`ParseAsync` は 1 件ごとに `GetCitiesAsync` を呼ぶ（`AddressService.cs:143`）。  
`GetCitiesAsync`（57〜79 行目）は `ja.json`（約 188 KB、47 都道府県・1898 市区町村）を  
毎回ディスクから読み込み・JSON デシリアライズしており、172 万件分繰り返されている。

`GetTownsAsync` / `LoadTownEntriesAsync` は `ConcurrentDictionary` でキャッシュ済みだが、  
`GetCitiesAsync` だけキャッシュが漏れており、実装の一貫性も取れていない。

---

## 対応内容

### 1. `_jaRoot` フィールドを追加し `LoadJaRootAsync` ヘルパーに集約する

- [x] `private JaRootResponse? _jaRoot;` フィールドを追加する
- [x] `LoadJaRootAsync(CancellationToken)` ヘルパーメソッドを実装する  
  - `_jaRoot` がすでにセットされていればそのまま返す  
  - ない場合は `LoadJsonAsync<JaRootResponse>` を呼び `_jaRoot` に格納する
- [x] `GetPrefecturesAsync`（35〜51 行目）を `LoadJaRootAsync` 経由に書き換える
- [x] `GetCitiesAsync`（58〜60 行目）を `LoadJaRootAsync` 経由に書き換える
- [x] `AggregateSubCityTownsAsync`（337〜338 行目）を `LoadJaRootAsync` 経由に書き換える

### 2. `GetCitiesAsync` の戻り値をキャッシュする

- [x] `private readonly ConcurrentDictionary<string, IReadOnlyList<City>> _cityCache = new();`  
  フィールドを追加する（`_townCache` / `_rawEntryCache` と同じ場所・命名規則）
- [x] `GetCitiesAsync` に `TryGetValue → なければ計算 → 格納` のパターンを実装する  
  （`_townCache` の実装パターンに揃えること）

### 3. 既存テストの全件パスを確認する

- [x] `dotnet test tests/JaAddress.Core.Tests/` がすべてグリーンになること

### 4. キャッシュ有効性テストを追加する

- [x] `GetPrefecturesAsync` を同一インスタンスで 2 回呼んだとき、返却リストが同一参照（`ReferenceEquals`）であること
- [x] `GetCitiesAsync` を同一都道府県名で 2 回呼んだとき、返却リストが同一参照であること
- [x] `ParseAsync` を同一都道府県の異なる住所で複数回呼んだあと、`GetCitiesAsync` の  
  内部呼び出しがキャッシュを使っていること（ファイルI/O が初回 1 回のみに限定されること）  
  ※ ファイル I/O の非発生はモック不要。`ReferenceEquals` でキャッシュ済みリストの  
  同一インスタンス返却を確認することで代替する。

---

## 実装上の注意

- `_jaRoot` はシングルスレッドで初期化されるわけではないが、  
  `GetPrefecturesAsync` 側の既存ロジック（`_prefectures` への代入）は非スレッドセーフなまま。  
  `_jaRoot` も同様に「最悪 2 回デシリアライズされても正しい値が返れば問題なし」とする。  
  `_prefectures` の既存パターンを踏襲し、`Interlocked` や `lock` は使わない。
- `_cityCache` はスレッドセーフが必要なため `ConcurrentDictionary` を使うこと。

---

## 完了条件

- [x] 全チェックボックスが完了している
- [x] `dotnet test` が全件グリーン（46件）
- [ ] レビュー・コミット済み
