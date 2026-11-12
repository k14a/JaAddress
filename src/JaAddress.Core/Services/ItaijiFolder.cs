using System.Collections.Frozen;
using JaAddress.Core.Options;
using Microsoft.Extensions.Logging;

namespace JaAddress.Core.Services;

/// <summary>
/// <see cref="IItaijiFolder"/> の既定実装。
/// 既定マップ（埋め込みリソース <c>Resources/itaiji.tsv</c>）を基点に、
/// <see cref="JaAddressOptions.ItaijiMapPath"/> で指定されたファイル、
/// データディレクトリ直下の <c>itaiji.local.tsv</c>、
/// <see cref="JaAddressOptions.AdditionalItaiji"/> の順にマージする（後勝ち）。
/// マップの読み込みはコンストラクタで一度だけ行う。
/// </summary>
internal sealed class ItaijiFolder : IItaijiFolder {
    private const string EmbeddedMapResourceName = "JaAddress.Core.Resources.itaiji.tsv";
    private const string LocalMapFileName = "itaiji.local.tsv";

    private readonly FrozenDictionary<char, char> _map;

    public ItaijiFolder(JaAddressOptions options, ILogger<ItaijiFolder> logger) {
        if (!options.FoldItaiji) {
            this._map = FrozenDictionary<char, char>.Empty;
            return;
        }

        var map = new Dictionary<char, char>();
        LoadFromEmbeddedResource(map, logger);

        if (!string.IsNullOrWhiteSpace(options.ItaijiMapPath)) {
            LoadFromFile(map, options.ItaijiMapPath!, logger);
        }

        if (!string.IsNullOrWhiteSpace(options.DataDirectory)) {
            var localPath = Path.Combine(options.DataDirectory, LocalMapFileName);
            if (File.Exists(localPath)) {
                LoadFromFile(map, localPath, logger);
            }
        }

        if (options.AdditionalItaiji is { Count: > 0 }) {
            foreach (var (from, to) in options.AdditionalItaiji) {
                map[from] = to;
            }
        }

        // 変換先が別エントリの変換元にもなっていると、畳み込みが冪等でなくなる（連鎖変換）。
        // AddressService は入力を一度だけ畳み込む前提のため、検出したら警告する。
        foreach (var (from, to) in map) {
            if (map.ContainsKey(to)) {
                logger.LogWarning("異体字マップに連鎖変換があります（'{From}'→'{To}'、かつ '{To2}' も別エントリの変換元）。畳み込み結果が不安定になる可能性があります。", from, to, to);
            }
        }

        this._map = map.ToFrozenDictionary();
        logger.LogInformation("異体字マップを読み込みました（{Count} 件）。", this._map.Count);
    }

    public bool Enabled => this._map.Count > 0;

    public IReadOnlyDictionary<char, char> Map => this._map;

    public string Fold(string value) {
        if (this._map.Count == 0 || string.IsNullOrEmpty(value)) {
            return value;
        }

        char[]? buffer = null;
        for (var i = 0; i < value.Length; i++) {
            if (this._map.TryGetValue(value[i], out var replacement)) {
                buffer ??= value.ToCharArray();
                buffer[i] = replacement;
            }
        }

        return buffer is null ? value : new string(buffer);
    }

    private static void LoadFromEmbeddedResource(Dictionary<char, char> map, ILogger logger) {
        var assembly = typeof(ItaijiFolder).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedMapResourceName);
        if (stream is null) {
            logger.LogWarning("既定の異体字マップリソース {Resource} が見つかりません。", EmbeddedMapResourceName);
            return;
        }

        using var reader = new StreamReader(stream);
        ParseInto(map, reader, EmbeddedMapResourceName, logger);
    }

    private static void LoadFromFile(Dictionary<char, char> map, string path, ILogger logger) {
        if (!File.Exists(path)) {
            logger.LogWarning("異体字マップファイルが見つかりません: {Path}", path);
            return;
        }

        using var reader = new StreamReader(path);
        ParseInto(map, reader, path, logger);
    }

    private static void ParseInto(Dictionary<char, char> map, TextReader reader, string source, ILogger logger) {
        string? line;
        var lineNumber = 0;
        while ((line = reader.ReadLine()) is not null) {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#') {
                continue;
            }

            var columns = trimmed.Split('\t');
            if (columns.Length < 2) {
                logger.LogWarning("異体字マップの書式不正（タブ区切りでない）: {Source}:{Line}", source, lineNumber);
                continue;
            }

            var from = columns[0];
            var to = columns[1];
            var fromLength = new System.Globalization.StringInfo(from).LengthInTextElements;
            var toLength = new System.Globalization.StringInfo(to).LengthInTextElements;
            if (fromLength != 1 || toLength != 1 || from.Length != 1 || to.Length != 1) {
                // サロゲートペア（char.Length==2）や複数文字は、住所のオフセット計算を壊すため受け付けない。
                logger.LogWarning("異体字マップのエントリは BMP の 1 文字 → 1 文字である必要があります（スキップ）: {Source}:{Line} '{From}'→'{To}'",
                    source, lineNumber, from, to);
                continue;
            }

            map[from[0]] = to[0];
        }
    }
}
