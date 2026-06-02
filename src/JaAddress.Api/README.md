# JaAddress API

日本の住所を正規化・分解する REST API です。
Swagger UI は起動後に `http://localhost:5000/swagger` で確認できます。

---

## 住所マスター

### `GET /prefectures` — 都道府県一覧

全都道府県の一覧を返します。

**レスポンス例**

```json
[
  {
    "code": 13000,
    "name": "東京都",
    "latitude": 35.6895,
    "longitude": 139.6917
  }
]
```

---

### `GET /prefectures/{pref}/cities` — 市区町村一覧

指定した都道府県の市区町村一覧を返します。
政令指定都市は区ごとに 1 件として返します（例: `さいたま市西区`、`さいたま市北区`）。
郡に属する町村は郡名付きで返します（例: `泉南郡熊取町`）。

| パラメータ | 種別 | 説明 |
|-----------|------|------|
| `pref` | パスパラメータ | 都道府県名（例: `大阪府`） |

**レスポンス例**

```json
[
  {
    "code": 27100,
    "prefectureName": "大阪府",
    "name": "大阪市北区",
    "county": null,
    "ward": "北区",
    "displayName": "大阪市北区",
    "latitude": 34.7024,
    "longitude": 135.51
  },
  {
    "code": 27361,
    "prefectureName": "大阪府",
    "name": "泉南郡熊取町",
    "county": "泉南郡",
    "ward": null,
    "displayName": "泉南郡熊取町",
    "latitude": 34.381,
    "longitude": 135.364
  }
]
```

**エラー**

| ステータス | 説明 |
|-----------|------|
| 404 | 指定した都道府県が存在しない |

---

### `GET /prefectures/{pref}/cities/{city}/towns` — 町字一覧

指定した市区町村の町字一覧を返します。

`city` に政令市名（`さいたま市`）または郡名（`泉南郡`）を指定すると、
配下の全区・全町村の町字をまとめて返します。

| パラメータ | 種別 | 説明 |
|-----------|------|------|
| `pref` | パスパラメータ | 都道府県名（例: `埼玉県`） |
| `city` | パスパラメータ | 市区町村名・政令市名・郡名（例: `さいたま市`、`泉南郡`、`泉南郡熊取町`） |

**`city` の指定パターン**

| 指定値 | 返される範囲 |
|--------|-------------|
| `さいたま市西区` | 西区の町字のみ |
| `さいたま市` | さいたま市 全区の町字 |
| `泉南郡熊取町` | 熊取町の町字のみ |
| `泉南郡` | 泉南郡 全町村の町字 |

**レスポンス例**

```json
[
  {
    "prefectureName": "大阪府",
    "cityName": "泉南郡熊取町",
    "name": "大字久保",
    "koaza": null,
    "latitude": 34.381,
    "longitude": 135.364
  }
]
```

---

## 住所パース

### `GET /parse` — 住所を 1 件パース

クエリ文字列で渡した住所を都道府県・市区町村・町字・番地に分解します。

| パラメータ | 種別 | デフォルト | 説明 |
|-----------|------|-----------|------|
| `q` | クエリ | 必須 | パース対象の住所文字列 |
| `bestEffort` | クエリ | `false` | `true` にすると文字列の途中から都道府県名を検索し、住所部分のみを抽出する |
| `normalizeOaza` | クエリ | `false` | `true` にすると「大字」が省略された入力でも「大字XXX」データと照合し、正規形を返す |

**レスポンス例（成功）**

```http
GET /parse?q=大阪府泉南郡熊取町大字久保2983-1&normalizeOaza=false
```

```json
{
  "input": "大阪府泉南郡熊取町大字久保2983-1",
  "prefecture": "大阪府",
  "city": "泉南郡熊取町",
  "county": "泉南郡",
  "town": "大字久保",
  "street": null,
  "block": "2983-1",
  "remainder": "",
  "offset": 0,
  "corrected": false,
  "success": true
}
```

**`normalizeOaza=true` を使う例**

入力に「大字」が付いていなくても、データ上の「大字XXX」に照合して正規化します。

```http
GET /parse?q=大阪府泉南郡熊取町久保2983-1&normalizeOaza=true
```

```json
{
  "town": "大字久保",
  "block": "2983-1",
  "corrected": false,
  "success": true
}
```

**`bestEffort=true` を使う例**

住所の前に余分なテキストがある場合に住所部分を抽出します。

```http
GET /parse?q=勤務先：東京都新宿区西新宿1丁目&bestEffort=true
```

```json
{
  "prefecture": "東京都",
  "city": "新宿区",
  "town": "西新宿",
  "street": "1丁目",
  "offset": 4,
  "success": true
}
```

**郡名・区名の省略補正**

不完全な住所でも可能な範囲で補正します。補正が行われた場合は `corrected: true` になります。

| 入力例 | 補正内容 |
|--------|---------|
| `大阪府熊取町大字久保2983-1` | 郡名「泉南郡」を補完 |
| `大阪府北区梅田1丁目` | 市名「大阪市」を補完 |
| `東京都西新宿1丁目` | 市区町村「新宿区」を補完 |

**エラー**

| ステータス | 説明 |
|-----------|------|
| 404 | 都道府県・市区町村を特定できなかった（`success: false` も同時に返る） |

---

### `POST /parse` — 住所を複数件パース（JSON）

JSON ボディで複数の住所を一括パースします。最大 20 件。

**リクエストボディ**

```json
{
  "addresses": [
    "大阪府泉南郡熊取町大字久保2983-1",
    "東京都新宿区西新宿1丁目2-3"
  ],
  "bestEffort": false,
  "normalizeOaza": false
}
```

| フィールド | 型 | デフォルト | 説明 |
|-----------|-----|-----------|------|
| `addresses` | `string[]` | 必須 | パース対象の住所リスト（最大 20 件） |
| `bestEffort` | `bool` | `false` | 前後テキストを含む文字列からの住所抽出 |
| `normalizeOaza` | `bool` | `false` | 「大字」省略入力の正規化 |

**レスポンス例**

```json
{
  "results": [
    {
      "input": "大阪府泉南郡熊取町大字久保2983-1",
      "prefecture": "大阪府",
      "city": "泉南郡熊取町",
      "county": "泉南郡",
      "town": "大字久保",
      "street": null,
      "block": "2983-1",
      "remainder": "",
      "offset": 0,
      "corrected": false,
      "success": true
    }
  ],
  "count": 1
}
```

**エラー**

| ステータス | 説明 |
|-----------|------|
| 400 | `addresses` が 20 件を超えている |

---

### `GET /parse/tsv/template` — TSV テンプレートのダウンロード

住所一括パース用の TSV テンプレートをダウンロードします。
1 行目がヘッダ行で、2 行目以降の `address` 列に住所を記入して `POST /parse/tsv` へアップロードします。

**レスポンス**

`Content-Type: text/tab-separated-values` でファイルを返します。

```
address	prefecture	city	county	town	street	block	remainder	corrected	offset
```

---

### `POST /parse/tsv` — TSV ファイルで住所を一括パース

TSV ファイルをアップロードして住所を一括パースし、結果 TSV を返します。
最大 10,000 件。

| パラメータ | 種別 | デフォルト | 説明 |
|-----------|------|-----------|------|
| `file` | フォームデータ | 必須 | TSV ファイル（`multipart/form-data`） |
| `bestEffort` | クエリ | `false` | 前後テキストを含む文字列からの住所抽出 |
| `normalizeOaza` | クエリ | `false` | 「大字」省略入力の正規化 |

**TSV フォーマット**

1 行目はヘッダ行（`GET /parse/tsv/template` で取得したファイルをそのまま使用）。
2 行目以降の 1 列目（`address`）に住所を 1 件ずつ記入します。

```
address
大阪府泉南郡熊取町大字久保2983-1
東京都新宿区西新宿1丁目2-3
```

**レスポンス**

`Content-Type: text/tab-separated-values` で結果ファイルを返します。
入力の `address` 列に加え、パース結果の各列が付加されます。

```
address	prefecture	city	county	town	street	block	remainder	corrected	offset
大阪府泉南郡熊取町大字久保2983-1	大阪府	泉南郡熊取町	泉南郡	大字久保		2983-1		
東京都新宿区西新宿1丁目2-3	東京都	新宿区		西新宿	1丁目	2-3		
```

**エラー**

| ステータス | 説明 |
|-----------|------|
| 400 | ファイルが空、またはヘッダ行がない |
| 400 | 行数が 10,000 件を超えている |

---

## レスポンスフィールド一覧

### パース結果（`ParseResultDto`）

| フィールド | 型 | 説明 |
|-----------|-----|------|
| `input` | `string` | 入力文字列 |
| `prefecture` | `string?` | 都道府県名（例: `大阪府`） |
| `city` | `string?` | 市区町村の完全名（例: `泉南郡熊取町`、`大阪市北区`） |
| `county` | `string?` | 郡名。郡に属する場合のみ設定（例: `泉南郡`） |
| `town` | `string?` | 町字名（例: `大字久保`、`西新宿`） |
| `street` | `string?` | 丁目（例: `1丁目`、`一丁目`） |
| `block` | `string?` | 番地（例: `2983-1`、`2-3`） |
| `remainder` | `string?` | パースできなかった残余文字列 |
| `offset` | `int` | `bestEffort=true` 時に都道府県名が見つかった位置（文字数） |
| `corrected` | `bool` | 郡名・市名・区名の省略補正が行われた場合 `true` |
| `success` | `bool` | 都道府県・市区町村の特定に成功した場合 `true` |
