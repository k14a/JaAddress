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

---

# TODO: 町名・番地の取りこぼし（2026-10-02、RivalDataCs TODO.md #45 の LBC 計測で発見）

## 背景

RivalDataCs の CorpNormalize 改修（RivalDataCs TODO.md #45）で、LBC（`m_dip_lbc`）1万件を `JaAddressNormalizer`
（`ParseAsync`、`SplitRemainder=true`・`NormalizeOaza=true`・`FoldItaiji=true`）で解析したところ、**約9.5%（950件）で番地を落としていた**。
LBC の addr1〜6 の区切りから作った正解（方式 (c')）と比較して判明。RivalDataCs Linkage の入力側（求人住所）の正規化にも同じ影響がある。
明細：`~/work/lbc_method_c_20261002.csv`（RivalDataCs 側の計測ツールの出力）。

## 原因（コードと辞書データで確認済み）

### 1. 町名のあとに小字（字○○）があると、小字と番地を両方落とす（約720件、最多）

- 例：`福島県伊達市保原町大泉字大地内95-5` → Town「保原町大泉」、Street/Block なし。
- `TryParseFromAsync` で町名「保原町大泉」が確定したあと、`SplitStreetBlock` に `字大地内95-5` が渡る。丁目でも数字始まりでもないため
  Block の正規表現（`^([0-9]+...)`）に一致せず、全体が Remainder になる。
- 辞書には小字のデータがある（`TownEntry.Koaza`。例：保原町大泉に `字一本杉`・`字宮前` 等。伊達市は 4,041 件中 3,912 件が koaza 付き）。
- 字の付かない小字（`中島`・`市木`・`117部` 等）も同様に落ちる。
- **出力形式は要決定**：`AddressParseResult` に小字を持つ項目がない。(a) `Koaza` プロパティを追加する、(b) Town 名に連結する、
  (c) 小字は読み飛ばして番地だけ取る、のいずれか。RivalDataCs 側の検索キー（町名｜丁目｜番地）の設計と合わせて決める。

### 2. 丁目のある町で、存在しない丁目番号の数字を番地から落とす（`都町13-9` → Block「9」）

- `SplitStreetBlock` の「丁目省略形」分岐（`^([0-9]+)[-－−ー](.*)$`）で、`chomeEntries` に該当する丁目番号（13）がなくても
  `afterStreet = Groups[2]` で先頭の数字を消費してしまう。日向市 `都町` は辞書に `一丁目` のみ。
- 修正方針：丁目番号が辞書に存在した場合だけ `afterStreet` を進める（存在しなければ入力全体を番地として扱う）。**明確なバグ。**

### 3. 「大字XXX」の大字を外した一致が、より長い町名より優先される（`本城東2丁目1-21` → Town「大字本城」）

- `FindTown` は町名を `Name.Length` の降順に調べるが、`NormalizeOaza` の比較では「大字」を外した長さ（`大字本城`→`本城`＝2文字）で
  一致を判定する。`大字本城`（4文字）が `本城東`（3文字）より先に調べられ、`本城` が前方一致してしまう。
- 修正方針：大字を外した場合の実際の消費文字数で比較し、消費文字数が最長の候補を選ぶ。**明確なバグ。**

### 4. 辞書の町名に旧町村名等の接頭辞があり、入力側が省略していると町名が見つからない（`紀北町相賀347-4`）

- 辞書の町名は `海山区相賀`（紀北町は旧町名「海山区」付きが正式表記）。LBC・入力側は `相賀` と書くため一致しない。
- 件数は少ない。対応するか・どう一致させるか（町名の末尾一致を候補にする等）は要検討。

## 対応内容

- [x] 2. 丁目省略形で存在しない丁目番号を消費しないよう修正し、テストを追加する（2026-10-02）
- [x] 3. `FindTown` で大字を外した一致の消費文字数を考慮して最長一致を選ぶよう修正し、テストを追加する（2026-10-02）。
      消費文字数が同じ場合（例：入力「本城123」が「本城」と「大字本城」の両方に2文字一致）は従来どおり Name.Length の長い方
      （「大字本城」）を優先し、挙動を変えていない（変えるかは別途検討）。
- [ ] 1. 小字の扱い（出力形式）を決めてから修正する（辞書の `Koaza` で一致させる、字の付かない小字も含めるか）
- [ ] 4. 旧町名等の接頭辞付き町名の扱いを決める
- [x] 修正後、RivalDataCs 側の計測ツールで同じ1万件を再計測し、番地の取りこぼし件数の変化を確認する（2026-10-02、2・3 の修正後）
      - 方式 (c') と入力側相当の検索キー一致率：全体 90.5% → **91.8%**、addr4 が「N丁目」99.0% → **99.9%**、addr4 空 95.6% → **97.6%**。
        番地の不一致 950件 → 824件。明細 `~/work/lbc_method_c_20261002_jafix.csv`。
      - 「N丁目」「addr4 空」で残った不一致（約95件）は、すべて町名が見つからず町名以下が空になるケース（4. と同類）：
        `相賀`（辞書は `海山区相賀`）、`五十嵐１の町`、`中戸蔦東６線` 等。
      - 残りの大半は 1.（小字）。
- [ ] RivalDataCs の Linkage 結果への影響（入力側の正規化が変わる）を確認する
