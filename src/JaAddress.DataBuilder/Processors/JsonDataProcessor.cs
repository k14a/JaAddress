using System.Text.Json;
using JaAddress.DataBuilder.Downloaders;
using JaAddress.DataBuilder.Models;
using Microsoft.Extensions.Logging;

namespace JaAddress.DataBuilder.Processors;

/// <summary>
/// Geoloniaから取得したJSONデータをローカルファイルに保存する。
/// ディレクトリ構造はAPIのURL構造と同一にする:
///   {outputDir}/ja.json
///   {outputDir}/ja/{都道府県名}/{市区町村名}.json
/// </summary>
internal sealed class JsonDataProcessor(ILogger<JsonDataProcessor> logger) {
    private static readonly JsonSerializerOptions WriteOptions = new() {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,  // 追加
    };

    private readonly ILogger<JsonDataProcessor> _logger = logger;


    /// <summary>
    /// 都道府県一覧データを保存する。
    /// </summary>
    public async Task SavePrefecturesAsync(JaRootResponse data, string outputDir, CancellationToken ct = default) {
        var path = Path.Combine(outputDir, "ja.json");
        await SaveJsonAsync(data, path, ct);
        this._logger.LogInformation("都道府県一覧を保存しました: {Path}", path);
    }

    /// <summary>
    /// 町字データを保存する。
    /// </summary>
    public async Task SaveTownsAsync(TownRootResponse data, string prefName, string cityName, string outputDir, CancellationToken ct = default) {
        var dir = Path.Combine(outputDir, "ja", prefName);
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"{cityName}.json");
        await SaveJsonAsync(data, path, ct);
        this._logger.LogDebug("町字データを保存しました: {Path}", path);
    }

    private static async Task SaveJsonAsync<T>(T data, string path, CancellationToken ct) {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);

        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, data, WriteOptions, ct);
    }
}
