using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JaAddress.Core.Models;
using JaAddress.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JaAddress.Core.Services;

internal sealed class AddressService(
    JaAddressOptions options,
    ILogger<AddressService> logger) : IAddressService {

    private readonly string _dataDir = options.DataDirectory;
    private readonly ILogger<AddressService> _logger = logger;

    // キャッシュ
    private IReadOnlyList<Prefecture>? _prefectures;
    private readonly ConcurrentDictionary<string, IReadOnlyList<Town>> _townCache = new();

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

        var path = Path.Combine(this._dataDir, "ja", prefName, $"{cityName}.json");
        this._logger.LogDebug("町字データを読み込み中: {Path}", path);

        var root = await LoadJsonAsync<TownRootResponse>(path, ct);
        if (root is null) {
            this._logger.LogWarning("町字データが見つかりません: {Path}", path);
            return [];
        }

        var towns = root.Data
            .Where(t => !string.IsNullOrEmpty(t.OazaCho))  // 大字・町名があるものだけ
            .GroupBy(t => t.OazaCho!)                        // 大字・町名でグループ化
            .Select(g => new Town {
                PrefectureName = prefName,
                CityName = cityName,
                Name = g.Key,
                Latitude = g.First().Point is { Length: > 1 } ? (decimal)g.First().Point[1] : null,
                Longitude = g.First().Point is { Length: > 0 } ? (decimal)g.First().Point[0] : null,
            })
            .ToList()
            .AsReadOnly();
        // var towns = root.Data
        //     .Select(t => new Town {
        //         PrefectureName = prefName,
        //         CityName       = cityName,
        //         Name           = t.Town,
        //         Koaza          = t.Koaza,
        //         Latitude       = t.Lat.HasValue ? (decimal)t.Lat.Value : null,
        //         Longitude      = t.Lng.HasValue ? (decimal)t.Lng.Value : null,
        //     })
        //     .ToList()
        //     .AsReadOnly();

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
        var prefecture = prefectures.FirstOrDefault(p => input.StartsWith(p.Name));
        if (prefecture is null) {
            this._logger.LogDebug("都道府県を特定できませんでした: {Address}", address);
            return null;
        }

        var afterPref = input[prefecture.Name.Length..];

        // 市区町村を特定
        var cities = await this.GetCitiesAsync(prefecture.Name, ct);
        var city = cities
            .OrderByDescending(c => c.DisplayName.Length) // 長い名前を優先（部分一致誤検知防止）
            .FirstOrDefault(c => afterPref.StartsWith(c.DisplayName));
        if (city is null) {
            this._logger.LogDebug("市区町村を特定できませんでした: {Address}", address);
            return null;
        }

        var remainder = afterPref[city.DisplayName.Length..];

        // SplitRemainder=false の場合はここで返す
        if (!options.SplitRemainder) {
            return new AddressParseResult {
                Prefecture = prefecture,
                City = city,
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
                Remainder = remainder,
            };
        }

        var afterTown = remainder[town.Name.Length..];
        var (street, block) = SplitStreetBlock(afterTown);

        return new AddressParseResult {
            Prefecture = prefecture,
            City = city,
            Town = town,
            Street = string.IsNullOrEmpty(street) ? null : street,
            Block = string.IsNullOrEmpty(block) ? null : block,
            Remainder = string.IsNullOrEmpty(street) ? afterTown : string.Empty, // 修正
        };
        // return new AddressParseResult {
        //     Prefecture = prefecture,
        //     City       = city,
        //     Town       = town,
        //     Street     = string.IsNullOrEmpty(street) ? null : street,
        //     Block      = string.IsNullOrEmpty(block)  ? null : block,
        //     Remainder  = afterTown,
        // };
    }

    // -------------------------------------------------------
    // ユーティリティ
    // -------------------------------------------------------

    /// <summary>全角数字・ハイフン類を半角に正規化する。</summary>
    private static string NormalizeNumber(string input) {
        // 全角数字 → 半角
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

    /// <summary>"1丁目2-3" → ("1丁目", "2-3") に分割する。</summary>
    private static (string Street, string Block) SplitStreetBlock(string input) {
        // [0-9] で半角数字のみにマッチさせる（\d は全角数字にもマッチするため使わない）
        var match = Regex.Match(input, @"^([0-9]+(?:丁目|番地|番))(.*)$");
        if (match.Success) {
            return (match.Groups[1].Value, match.Groups[2].Value.TrimStart('-', '－'));
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
