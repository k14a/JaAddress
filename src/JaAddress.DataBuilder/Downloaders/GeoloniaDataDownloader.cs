using System.Net.Http.Json;
using System.Text.Json;
using JaAddress.DataBuilder.Models;
using Microsoft.Extensions.Logging;

namespace JaAddress.DataBuilder.Downloaders;

/// <summary>
/// Geolonia 住所データ API からデータを取得する。
/// リトライと並列度制御を内蔵する。
/// </summary>
internal sealed class GeoloniaDataDownloader(HttpClient http, ILogger<GeoloniaDataDownloader> logger) {
    private const string BaseUrl = "https://japanese-addresses-v2.geoloniamaps.com/api";
    private const int MaxRetry = 3;
    private const int RetryDelayMs = 1000;

    private readonly HttpClient _http = http;
    private readonly ILogger<GeoloniaDataDownloader> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 都道府県・市区町村一覧 (ja.json) を取得する。
    /// </summary>
    public async Task<JaRootResponse> GetPrefecturesAsync(CancellationToken ct = default) {
        var url = $"{BaseUrl}/ja.json";
        this._logger.LogInformation("都道府県一覧を取得中: {Url}", url);

        return await this.FetchWithRetryAsync<JaRootResponse>(url, ct)
            ?? throw new InvalidOperationException("都道府県一覧の取得に失敗しました。");
    }

    /// <summary>
    /// 指定した都道府県・市区町村の町字データを取得する。
    /// </summary>
    public async Task<TownRootResponse?> GetTownsAsync( string prefName, string cityName, CancellationToken ct = default) {
        var encodedPref = Uri.EscapeDataString(prefName);
        var encodedCity = Uri.EscapeDataString(cityName);
        var url = $"{BaseUrl}/ja/{encodedPref}/{encodedCity}.json";

        this._logger.LogDebug("町字データを取得中: {Pref} {City}", prefName, cityName);

        return await this.FetchWithRetryAsync<TownRootResponse>(url, ct);
    }

    /// <summary>
    /// 指定URLからJSONを取得する。失敗した場合は MaxRetry 回リトライする。
    /// </summary>
    private async Task<T?> FetchWithRetryAsync<T>(string url, CancellationToken ct) {
        for (var attempt = 1; attempt <= MaxRetry; attempt++) {
            try {
                var response = await this._http.GetAsync(url, ct);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    this._logger.LogWarning("データが存在しません (404): {Url}", url);
                    return default;
                }

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) when (attempt < MaxRetry) {
                this._logger.LogWarning( ex, "取得失敗 (試行 {Attempt}/{Max}): {Url}", attempt, MaxRetry, url);

                await Task.Delay(RetryDelayMs * attempt, ct);
            } catch (Exception ex) {
                this._logger.LogError(ex, "取得に失敗しました: {Url}", url);
                throw;
            }
        }

        return default;
    }
}
