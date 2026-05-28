using JaAddress.DataBuilder.Downloaders;
using JaAddress.DataBuilder.Processors;
using Microsoft.Extensions.Logging;

namespace JaAddress.DataBuilder.Commands;

/// <summary>
/// Geoloniaからデータをダウンロードしてローカルに保存するコマンド。
/// </summary>
internal sealed class BuildDataCommand(
    GeoloniaDataDownloader downloader,
    JsonDataProcessor processor,
    ILogger<BuildDataCommand> logger) {

    private readonly GeoloniaDataDownloader _downloader = downloader;
    private readonly JsonDataProcessor _processor = processor;
    private readonly ILogger<BuildDataCommand> _logger = logger;

    /// <summary>
    /// 町字データ取得の並列度。
    /// 大量のリクエストを同時に送らないよう制限する。
    /// </summary>
    private const int MaxDegreeOfParallelism = 5;

    /// <summary>
    /// データ構築処理を実行する。
    /// </summary>
    /// <param name="outputDir">データ出力先ディレクトリ</param>
    /// <param name="prefFilter">
    /// 都道府県名フィルター（部分一致）。null の場合は全都道府県を取得。
    /// （例："東京都" を指定すると東京都のみ取得）
    /// </param>
    /// <param name="ct">キャンセルトークン</param>
    public async Task ExecuteAsync( string outputDir, string? prefFilter = null, CancellationToken ct = default) {
        this._logger.LogInformation("データ構築を開始します。出力先: {OutputDir}", outputDir);
        Directory.CreateDirectory(outputDir);

        // Step 1: 都道府県一覧を取得・保存
        var prefData = await this._downloader.GetPrefecturesAsync(ct);
        await this._processor.SavePrefecturesAsync(prefData, outputDir, ct);

        // フィルタリング
        var targets = prefData.Data
            .Where(p => prefFilter is null || p.Pref.Contains(prefFilter, StringComparison.Ordinal))
            .ToList();

        this._logger.LogInformation(
            "対象都道府県: {Count} 件 / 全 {Total} 件",
            targets.Count, prefData.Data.Count);

        // Step 2: 市区町村ごとに町字データを取得・保存
        // 都道府県単位でループし、市区町村は並列で処理する
        var totalCities = targets.Sum(p => p.Cities.Count);
        var processed = 0;

        foreach (var pref in targets) {
            this._logger.LogInformation(
                "[{Pref}] {Count} 市区町村の町字データを取得します",
                pref.Pref, pref.Cities.Count);

            // 並列度を制限しながら市区町村を処理
            var options = new ParallelOptions {
                MaxDegreeOfParallelism = MaxDegreeOfParallelism,
                CancellationToken = ct,
            };

            await Parallel.ForEachAsync(pref.Cities, options, async (city, innerCt) => {
                var cityDisplayName = city.Ward is null ? city.City : $"{city.City}{city.Ward}";

                var townData = await this._downloader.GetTownsAsync(pref.Pref, cityDisplayName, innerCt);

                if (townData is not null) {
                    await this._processor.SaveTownsAsync(
                        townData,
                        pref.Pref,
                        cityDisplayName,
                        outputDir,
                        innerCt);
                }

                var current = Interlocked.Increment(ref processed);
                if (current % 50 == 0) {
                    this._logger.LogInformation("進捗: {Current}/{Total} 市区町村", current, totalCities);
                }
            });
        }

        this._logger.LogInformation(
            "データ構築が完了しました。{Total} 市区町村のデータを保存しました。",
            processed);
    }
}
