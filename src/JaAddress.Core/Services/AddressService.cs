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
                Name = c.City,
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

        if (entries.Count == 0) {
            this._townCache[cacheKey] = [];
            return [];
        }

        var towns = entries
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

        // 市区名省略の補正: "大宮区…" → ward 単体でマッチして親 city を補完
        var corrected = false;
        if (city is null) {
            city = cities
                .Where(c => c.Ward is not null)
                .OrderByDescending(c => c.Ward!.Length)
                .FirstOrDefault(c => afterPref.StartsWith(c.Ward!));
            if (city is not null) {
                corrected = true;
                this._logger.LogDebug("市区名省略を補正しました: {Ward} → {DisplayName}", city.Ward, city.DisplayName);
            }
        }

        if (city is null) {
            this._logger.LogDebug("市区町村を特定できませんでした: {Address}", address);
            return null;
        }

        // 補正時は ward 分だけ、通常時は DisplayName 分を消費
        var consumedLength = corrected && city.Ward is not null
            ? city.Ward.Length
            : city.DisplayName.Length;
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
        var towns = await this.GetTownsAsync(prefecture.Name, city.Name, ct);
        var town = towns
            .OrderByDescending(t => t.Name.Length)
            .FirstOrDefault(t => remainder.StartsWith(t.Name));

        if (town is null) {
            return new AddressParseResult {
                Prefecture = prefecture,
                City = city,
                Corrected = corrected,
                Offset = offset,
                Remainder = remainder,
            };
        }

        var afterTown = remainder[town.Name.Length..];

        // 丁目名をデータから引くために生エントリを使う（GetTownsAsync 内でキャッシュ済み）
        var rawEntries = await this.LoadTownEntriesAsync(prefecture.Name, city.Name, ct);
        var chomeEntries = rawEntries.Where(e => e.OazaCho == town.Name).ToList();
        var (street, block) = SplitStreetBlock(afterTown, chomeEntries);

        return new AddressParseResult {
            Prefecture = prefecture,
            City = city,
            Corrected = corrected,
            Offset = offset,
            Town = town,
            Street = string.IsNullOrEmpty(street) ? null : street,
            Block = string.IsNullOrEmpty(block) ? null : block,
            Remainder = string.IsNullOrEmpty(street) ? afterTown : string.Empty,
        };
    }

    // -------------------------------------------------------
    // ユーティリティ
    // -------------------------------------------------------

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
                    '－' or '―' or '‐' or '–' => '-',
                    _ => src[i],
                };
            }
        });
        return result;
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

    /// <summary>"1丁目2-3" または "3-2-1" を (Street, Block) に分割する。</summary>
    private static (string Street, string Block) SplitStreetBlock(
        string input, IReadOnlyList<TownEntry> chomeEntries) {

        // 明示的な丁目/番地/番: "1丁目2-3" → ("1丁目", "2-3")
        // [0-9] で半角数字のみにマッチさせる（\d は全角数字にもマッチするため使わない）
        var match = Regex.Match(input, @"^([0-9]+(?:丁目|番地|番))(.*)$");
        if (match.Success) {
            return (match.Groups[1].Value, match.Groups[2].Value.TrimStart('-', '－'));
        }

        // 省略記法: "3-2-1" → chome_n でデータを検索して漢字丁目名を返す
        var bareMatch = Regex.Match(input, @"^([0-9]+)([-－].+)$");
        if (bareMatch.Success && int.TryParse(bareMatch.Groups[1].Value, out var chomeN)) {
            var chomeEntry = chomeEntries.FirstOrDefault(e => e.ChomeN == chomeN);
            var street = chomeEntry?.Chome ?? (bareMatch.Groups[1].Value + "丁目");
            return (street, bareMatch.Groups[2].Value.TrimStart('-', '－'));
        }

        return (string.Empty, string.Empty);
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
