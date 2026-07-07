using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JaAddress.Core.Models;
using JaAddress.Core.Options;
using Microsoft.Extensions.Logging;

namespace JaAddress.Core.Services;

internal sealed class AddressService(
    JaAddressOptions options,
    ILogger<AddressService> logger) : IAddressService {

    private readonly string _dataDir = options.DataDirectory;
    private readonly ILogger<AddressService> _logger = logger;

    // キャッシュ
    private IReadOnlyList<Prefecture>? _prefectures;
    private readonly ConcurrentDictionary<string, IReadOnlyList<Town>> _townCache = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<TownEntry>> _rawEntryCache = new();

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true,
    };

    // -------------------------------------------------------
    // 都道府県一覧
    // -------------------------------------------------------
    public async Task<IReadOnlyList<Prefecture>> GetPrefecturesAsync(CancellationToken ct = default) {
        if (this._prefectures is not null) {
            return this._prefectures;
        }

        var path = Path.Combine(this._dataDir, "ja.json");
        this._logger.LogDebug("都道府県データを読み込み中: {Path}", path);

        var root = await LoadJsonAsync<JaRootResponse>(path, ct)
            ?? throw new InvalidOperationException($"都道府県データの読み込みに失敗しました: {path}");

        this._prefectures = root.Data
            .Select(p => new Prefecture {
                Code = p.Code,
                Name = p.Pref,
                Latitude = p.Point.Length > 1 ? (decimal)p.Point[1] : 0m,
                Longitude = p.Point.Length > 0 ? (decimal)p.Point[0] : 0m,
            })
            .ToList()
            .AsReadOnly();

        return this._prefectures;
    }

    // -------------------------------------------------------
    // 市区町村一覧
    // -------------------------------------------------------
    public async Task<IReadOnlyList<City>> GetCitiesAsync(string prefName, CancellationToken ct = default) {
        var path = Path.Combine(this._dataDir, "ja.json");
        var root = await LoadJsonAsync<JaRootResponse>(path, ct)
            ?? throw new InvalidOperationException($"都道府県データの読み込みに失敗しました: {path}");

        var pref = root.Data.FirstOrDefault(p => p.Pref == prefName)
            ?? throw new ArgumentException($"都道府県が見つかりません: {prefName}");

        return pref.Cities
            .Select(c => new City {
                Code = c.Code,
                PrefectureName = prefName,
                Name = c.Ward is not null ? $"{c.City}{c.Ward}" :
                       c.County is not null ? $"{c.County}{c.City}" :
                       c.City,
                County = c.County,
                Ward = c.Ward,
                Latitude = c.Point.Length > 1 ? (decimal)c.Point[1] : 0m,
                Longitude = c.Point.Length > 0 ? (decimal)c.Point[0] : 0m,
            })
            .ToList()
            .AsReadOnly();
    }

    // -------------------------------------------------------
    // 町字一覧
    // -------------------------------------------------------
    public async Task<IReadOnlyList<Town>> GetTownsAsync(
        string prefName, string cityName, CancellationToken ct = default) {

        var cacheKey = $"{prefName}/{cityName}";
        if (this._townCache.TryGetValue(cacheKey, out var cached)) {
            return cached;
        }

        var entries = await this.LoadTownEntriesAsync(prefName, cityName, ct);

        IReadOnlyList<Town> towns;
        if (entries.Count > 0) {
            towns = EntriesToTowns(prefName, cityName, entries);
        } else {
            // ファイルが見つからない場合は政令市（区）または郡として結合する
            // 各サブ市区町村の GetTownsAsync を再帰的に呼び出すことで
            // Town.CityName に正しい区・町村名がセットされる
            towns = await this.AggregateSubCityTownsAsync(prefName, cityName, ct);
        }

        this._townCache[cacheKey] = towns;
        return towns;
    }

    // -------------------------------------------------------
    // 住所パース
    // -------------------------------------------------------
    public async Task<AddressParseResult?> ParseAsync(
        string address,
        AddressParseOptions? options = null,
        CancellationToken ct = default) {

        options ??= new AddressParseOptions();
        var input = options.NormalizeNumber ? NormalizeNumber(address) : address;

        // 都道府県を特定
        var prefectures = await this.GetPrefecturesAsync(ct);
        Prefecture? prefecture;
        int offset;

        if (options.BestEffort) {
            (prefecture, offset) = FindPrefectureInText(input, prefectures);
            if (offset > 0) {
                input = input[offset..];
                this._logger.LogDebug("BestEffort: offset={Offset} で住所を検出しました: {Address}", offset, address);
            }
        } else {
            prefecture = prefectures.FirstOrDefault(p => input.StartsWith(p.Name));
            offset = 0;
        }

        if (prefecture is null) {
            this._logger.LogDebug("都道府県を特定できませんでした: {Address}", address);
            return null;
        }

        var afterPref = input[prefecture.Name.Length..];

        // 市区町村を特定
        var cities = await this.GetCitiesAsync(prefecture.Name, ct);
        var city = cities
            .OrderByDescending(c => c.DisplayName.Length)
            .FirstOrDefault(c => afterPref.StartsWith(c.DisplayName));

        var corrected = false;
        Town? correctedTown = null;

        // 補正1: ward省略 "北区…" → ward 単体でマッチして親 city を補完
        if (city is null) {
            city = cities
                .Where(c => c.Ward is not null)
                .OrderByDescending(c => c.Ward!.Length)
                .FirstOrDefault(c => afterPref.StartsWith(c.Ward!));
            if (city is not null) {
                corrected = true;
                this._logger.LogDebug("区名省略を補正しました: {Ward} → {DisplayName}", city.Ward, city.DisplayName);
            }
        }

        // 補正2: city完全省略 "西新宿…" → 町字から市区町村を逆引き
        if (city is null) {
            (city, correctedTown) = await this.FindCityByTownAsync(prefecture.Name, afterPref, cities, ct);
            if (city is not null) {
                corrected = true;
                this._logger.LogDebug("市区町村省略を補正しました: {Town} → {City}", correctedTown!.Name, city.DisplayName);
            }
        }

        // 補正3: county省略 "熊取町…" → county なしで市区町村をマッチして補完
        if (city is null) {
            city = cities
                .Where(c => c.County is not null)
                .OrderByDescending(c => c.Name.Length - c.County!.Length)
                .FirstOrDefault(c => afterPref.StartsWith(c.Name[c.County!.Length..]));
            if (city is not null) {
                corrected = true;
                this._logger.LogDebug("郡名省略を補正しました: {City}", city.DisplayName);
            }
        }

        if (city is null) {
            this._logger.LogDebug("市区町村を特定できませんでした: {Address}", address);
            return null;
        }

        // 消費長: city完全省略=0、ward省略=ward長、county省略=郡名を除いた市区町村名長、通常=DisplayName長
        var consumedLength =
            correctedTown is not null ? 0 :
            corrected && city.Ward is not null ? city.Ward.Length :
            corrected && city.County is not null ? city.Name.Length - city.County.Length :
            city.DisplayName.Length;
        var remainder = afterPref[consumedLength..];

        // SplitRemainder=false の場合はここで返す
        if (!options.SplitRemainder) {
            return new AddressParseResult {
                Prefecture = prefecture,
                City = city,
                Corrected = corrected,
                Offset = offset,
                Remainder = remainder,
            };
        }

        // 町字・Street・Block を分割
        // city逆引き補正時はすでに town が確定しているため再検索しない
        Town? town;
        int consumedTownLength;
        if (correctedTown is not null) {
            town = correctedTown;
            consumedTownLength = town.Name.Length;
        } else {
            var towns = await this.GetTownsAsync(prefecture.Name, city.Name, ct);
            (town, consumedTownLength) = FindTown(remainder, towns, options.NormalizeOaza);
        }

        if (town is null) {
            return new AddressParseResult {
                Prefecture = prefecture,
                City = city,
                Corrected = corrected,
                Offset = offset,
                Remainder = remainder,
            };
        }

        var afterTown = remainder[consumedTownLength..];

        // 丁目名をデータから引くために生エントリを使う（GetTownsAsync 内でキャッシュ済み）
        var rawEntries = await this.LoadTownEntriesAsync(prefecture.Name, city.Name, ct);
        var chomeEntries = rawEntries.Where(e => e.OazaCho == town.Name).ToList();
        var (street, block, tail) = SplitStreetBlock(afterTown, chomeEntries);

        return new AddressParseResult {
            Prefecture = prefecture,
            City = city,
            Corrected = corrected,
            Offset = offset,
            Town = town,
            Street = string.IsNullOrEmpty(street) ? null : street,
            Block = string.IsNullOrEmpty(block) ? null : block,
            Remainder = string.IsNullOrEmpty(street) && string.IsNullOrEmpty(block) ? afterTown : tail,
        };
    }

    // -------------------------------------------------------
    // ユーティリティ
    // -------------------------------------------------------

    /// <summary>
    /// 全市区町村の町字データを順に検索し、入力文字列の先頭に一致する town を持つ city を返す。
    /// 市区町村名が完全省略された住所の補正に使用する。
    /// </summary>
    private async Task<(City? City, Town? Town)> FindCityByTownAsync(
        string prefName,
        string input,
        IReadOnlyList<City> cities,
        CancellationToken ct) {

        foreach (var city in cities.OrderByDescending(c => c.DisplayName.Length)) {
            var towns = await this.GetTownsAsync(prefName, city.Name, ct);
            var town = towns
                .Where(t => t.Name.Length >= 2)
                .OrderByDescending(t => t.Name.Length)
                .FirstOrDefault(t => input.StartsWith(t.Name));
            if (town is not null) {
                return (city, town);
            }
        }
        return (null, null);
    }

    /// <summary>
    /// 入力文字列中で最初に出現する都道府県名とその位置を返す。
    /// 見つからない場合は (null, 0)。
    /// </summary>
    private static (Prefecture? Prefecture, int Offset) FindPrefectureInText(
        string input, IReadOnlyList<Prefecture> prefectures) {

        Prefecture? found = null;
        var foundOffset = int.MaxValue;

        foreach (var pref in prefectures) {
            var idx = input.IndexOf(pref.Name, StringComparison.Ordinal);
            if (idx >= 0 && idx < foundOffset) {
                found = pref;
                foundOffset = idx;
            }
        }

        return (found, found is not null ? foundOffset : 0);
    }

    /// <summary>全角数字・ハイフン類を半角に正規化する。</summary>
    private static string NormalizeNumber(string input) {
        var result = string.Create(input.Length, input, (span, src) => {
            for (var i = 0; i < src.Length; i++) {
                span[i] = src[i] switch {
                    >= '０' and <= '９' => (char)(src[i] - '０' + '0'),
                    '－' or '―' or '‐' or '–' or '−' or 'ー' => '-',
                    _ => src[i],
                };
            }
        });
        return result;
    }

    /// <summary>TownEntry リストを Town リストに変換する。</summary>
    private static IReadOnlyList<Town> EntriesToTowns(
        string prefName, string cityName, IReadOnlyList<TownEntry> entries) =>
        entries
            .Where(t => !string.IsNullOrEmpty(t.OazaCho))
            .GroupBy(t => t.OazaCho!)
            .Select(g => {
                var pt = g.First().Point;
                return new Town {
                    PrefectureName = prefName,
                    CityName = cityName,
                    Name = g.Key,
                    Latitude = pt is { Length: > 1 } ? (decimal)pt[1] : null,
                    Longitude = pt is { Length: > 0 } ? (decimal)pt[0] : null,
                };
            })
            .ToList()
            .AsReadOnly();

    /// <summary>
    /// 政令市名（区なし）または郡名に対してサブ市区町村の GetTownsAsync を再帰的に呼び出し
    /// 全町字を結合して返す。Town.CityName には各区・町村名が正しくセットされる。
    /// </summary>
    private async Task<IReadOnlyList<Town>> AggregateSubCityTownsAsync(
        string prefName, string baseName, CancellationToken ct) {

        var jaPath = Path.Combine(this._dataDir, "ja.json");
        var root = await LoadJsonAsync<JaRootResponse>(jaPath, ct);
        if (root is null) { return []; }

        var pref = root.Data.FirstOrDefault(p => p.Pref == prefName);
        if (pref is null) { return []; }

        // 政令市（区あり）: City == baseName && Ward != null
        List<string> subNames = [.. pref.Cities
            .Where(c => c.City == baseName && c.Ward is not null)
            .Select(c => $"{c.City}{c.Ward}")];

        // 郡: County == baseName
        if (subNames.Count == 0) {
            subNames = [.. pref.Cities
                .Where(c => c.County == baseName)
                .Select(c => $"{c.County}{c.City}")];
        }

        if (subNames.Count == 0) { return []; }

        this._logger.LogDebug(
            "{BaseName} のサブ市区町村 {Count} 件を結合します", baseName, subNames.Count);

        var allTowns = new List<Town>();
        foreach (var subName in subNames) {
            var subTowns = await this.GetTownsAsync(prefName, subName, ct);
            allTowns.AddRange(subTowns);
        }
        return allTowns.AsReadOnly();
    }

    /// <summary>市区町村の町字データ（生エントリ）を読み込みキャッシュする。</summary>
    private async Task<IReadOnlyList<TownEntry>> LoadTownEntriesAsync(
        string prefName, string cityName, CancellationToken ct) {

        var cacheKey = $"{prefName}/{cityName}";
        if (this._rawEntryCache.TryGetValue(cacheKey, out var cached)) {
            return cached;
        }

        var path = Path.Combine(this._dataDir, "ja", prefName, $"{cityName}.json");
        this._logger.LogDebug("町字データを読み込み中: {Path}", path);

        var root = await LoadJsonAsync<TownRootResponse>(path, ct);
        if (root is null) {
            this._logger.LogWarning("町字データが見つかりません: {Path}", path);
        }

        IReadOnlyList<TownEntry> entries = root is not null ? root.Data.AsReadOnly() : [];
        this._rawEntryCache[cacheKey] = entries;
        return entries;
    }

    /// <summary>
    /// remainder の先頭に一致する町字を探す。
    /// NormalizeOaza=true の場合、「大字XXX」データへの「XXX」入力も一致とみなし
    /// 正規形（大字付き）の Town を返す。戻り値の ConsumedLength は入力から消費した文字数。
    /// </summary>
    private static (Town? Town, int ConsumedLength) FindTown(
        string remainder, IReadOnlyList<Town> towns, bool normalizeOaza) {

        // 名前が長い順に検索（長い名前を優先して誤検知を防ぐ）
        foreach (var t in towns.OrderByDescending(t => t.Name.Length)) {
            if (remainder.StartsWith(t.Name)) {
                return (t, t.Name.Length);
            }
            if (normalizeOaza && t.Name.Length > 2 && t.Name.StartsWith("大字")
                && remainder.StartsWith(t.Name[2..])) {
                return (t, t.Name.Length - 2);
            }
        }
        return (null, 0);
    }

    /// <summary>
    /// "1丁目2-3建物名" や "3-2-1建物名" を (Street, Block, Tail) に分割する。
    /// Tail には番地以降の建物名・フロア等が入る。
    /// </summary>
    private static (string Street, string Block, string Tail) SplitStreetBlock(
        string input, IReadOnlyList<TownEntry> chomeEntries) {

        string street = string.Empty;
        string afterStreet = input;

        // 明示的な丁目: "1丁目..." → street="1丁目"
        // 番地/番は street ではなく block に属するため対象外
        // [0-9] で半角数字のみにマッチさせる（\d は全角数字にもマッチするため使わない）
        var explicitMatch = Regex.Match(input, @"^([0-9]+丁目)(.*)$");
        if (explicitMatch.Success) {
            street = explicitMatch.Groups[1].Value;
            afterStreet = explicitMatch.Groups[2].Value.TrimStart('-', '－', '−', 'ー');
        } else if (chomeEntries.Any(e => e.ChomeN is not null)) {
            // 丁目省略形（丁目あり地区のみ）: "3-..." → 入力の半角数字 + "丁目" に変換
            // chomeEntry.Chome（漢字形）ではなく入力数字を使うことで出力形式を統一する
            var bareChomeMatch = Regex.Match(input, @"^([0-9]+)[-－−ー](.*)$");
            if (bareChomeMatch.Success && int.TryParse(bareChomeMatch.Groups[1].Value, out var chomeN)) {
                if (chomeEntries.Any(e => e.ChomeN == chomeN)) {
                    street = bareChomeMatch.Groups[1].Value + "丁目";
                }
                afterStreet = bareChomeMatch.Groups[2].Value;
            } else {
                // 漢字丁目形（"一丁目..."）: chomeEntries.Chome と前方一致して "N丁目" に変換
                var kanjiEntry = chomeEntries
                    .Where(e => e.Chome is not null)
                    .OrderByDescending(e => e.Chome!.Length)
                    .FirstOrDefault(e => input.StartsWith(e.Chome!));
                if (kanjiEntry is not null) {
                    street = kanjiEntry.ChomeN.HasValue ? kanjiEntry.ChomeN.Value + "丁目" : kanjiEntry.Chome!;
                    afterStreet = input[kanjiEntry.Chome!.Length..].TrimStart('-', '－', '−', 'ー');
                }
            }
        }

        if (afterStreet.Length == 0) {
            return (street, string.Empty, string.Empty);
        }

        // "N番(地?)M号?" → "N-M" に正規化（例: "1番20号" → "1-20"、"7番地5" → "7-5"）
        var blockInput = Regex.Replace(afterStreet, @"^([0-9]+)番地?([0-9]+)号?", "$1-$2");
        // "N番(地?)" 単独（後続数字なし）→ "N" に正規化して 番/番地 を除去
        blockInput = Regex.Replace(blockInput, @"^([0-9]+)番地?", "$1");
        // 番地と建物名を分離: "[0-9]+(-[0-9]+)*" を番地、残りを建物名等として返す
        var blockMatch = Regex.Match(blockInput, @"^([0-9]+(?:[-－−ー][0-9]+)*)(.*)$");
        if (blockMatch.Success) {
            var tail = blockMatch.Groups[2].Value.TrimStart('-', '－', '−', 'ー').Trim(' ', '　');
            return (street, blockMatch.Groups[1].Value, tail);
        }

        // 番地なし（建物名・フロア等が続く場合）
        return (street, string.Empty, afterStreet);
    }

    private static async Task<T?> LoadJsonAsync<T>(string path, CancellationToken ct) {
        if (!File.Exists(path)) {
            return default;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }

    // -------------------------------------------------------
    // 内部JSONモデル（DataBuilderのModelsとは分離）
    // -------------------------------------------------------
    private sealed class JaRootResponse {
        public List<JaPrefecture> Data { get; init; } = [];
    }
    private sealed class JaPrefecture {
        public int Code { get; init; }
        public string Pref { get; init; } = string.Empty;
        public double[] Point { get; init; } = [];
        public List<JaCity> Cities { get; init; } = [];
    }
    private sealed class JaCity {
        public int Code { get; init; }
        public string? County { get; init; }
        public string City { get; init; } = string.Empty;
        public string? Ward { get; init; }
        public double[] Point { get; init; } = [];
    }
    private sealed class TownRootResponse {
        public List<TownEntry> Data { get; init; } = [];
    }
    private sealed class TownEntry {
        [JsonPropertyName("oaza_cho")]
        public string? OazaCho { get; init; }

        [JsonPropertyName("chome")]
        public string? Chome { get; init; }

        [JsonPropertyName("chome_n")]
        public int? ChomeN { get; init; }

        [JsonPropertyName("koaza")]
        public string? Koaza { get; init; }

        public double[]? Point { get; init; }
    }
}
