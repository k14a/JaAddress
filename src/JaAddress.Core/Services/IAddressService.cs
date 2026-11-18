using JaAddress.Core.Models;

namespace JaAddress.Core.Services;

/// <summary>
/// 住所の解析と、住所データ（都道府県・市区町村・町字）の参照を行うサービス。
/// <see cref="ServiceCollectionExtensions.AddJaAddress"/> で登録し、DI から取得する。
/// データは初回の呼び出しで読み込み、以降はメモリに保持する。
/// </summary>
public interface IAddressService {
    /// <summary>すべての都道府県を返す。</summary>
    /// <param name="ct">キャンセルトークン。</param>
    Task<IReadOnlyList<Prefecture>> GetPrefecturesAsync(CancellationToken ct = default);
    /// <summary>都道府県に属する市区町村を返す。</summary>
    /// <param name="prefName">都道府県名。例: "東京都"</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <exception cref="ArgumentException"><paramref name="prefName"/> の都道府県がデータにない場合。</exception>
    Task<IReadOnlyList<City>> GetCitiesAsync(string prefName, CancellationToken ct = default);
    /// <summary>
    /// 市区町村に属する町字を返す。政令指定都市名（区なし）や郡名を指定すると、配下の区・町村の町字をまとめて返す。
    /// </summary>
    /// <param name="prefName">都道府県名。例: "大阪府"</param>
    /// <param name="cityName">市区町村の完全名（<see cref="City.Name"/>）、政令指定都市名、または郡名。例: "大阪市北区"</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task<IReadOnlyList<Town>> GetTownsAsync(string prefName, string cityName, CancellationToken ct = default);
    /// <summary>
    /// 住所文字列を都道府県・市区町村・町字などに分けて解析する。
    /// 失敗した理由も知りたい場合は <see cref="ParseWithReasonAsync"/> を使う。
    /// </summary>
    /// <param name="address">解析する住所文字列。例: "東京都千代田区丸の内1-2-3"</param>
    /// <param name="options">解析の設定。null の場合は既定値を使う。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>解析結果。都道府県または市区町村を特定できなかった場合は null。</returns>
    Task<AddressParseResult?> ParseAsync(string address, AddressParseOptions? options = null, CancellationToken ct = default);
    /// <summary>
    /// <see cref="ParseAsync"/> と同じ解析を行い、解析できなかった場合はその理由も返す。
    /// </summary>
    /// <param name="address">解析する住所文字列。</param>
    /// <param name="options">解析の設定。null の場合は既定値を使う。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task<AddressParseOutcome> ParseWithReasonAsync(string address, AddressParseOptions? options = null, CancellationToken ct = default);
}
