using JaAddress.Domain.ValueObjects;

namespace JaAddress.Domain.Interfaces;

/// <summary>
/// 住所データの取得元を抽象化するインターフェース。
/// ローカルファイル実装とHTTP実装を差し替え可能にする。
/// </summary>
public interface IAddressDataSource
{
    /// <summary>
    /// 都道府県と市区町村の一覧を取得する。
    /// </summary>
    Task<PrefectureData> GetPrefecturesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定した都道府県・市区町村の町字一覧を取得する。
    /// </summary>
    /// <param name="prefName">都道府県名（例：東京都）</param>
    /// <param name="cityName">市区町村名（例：新宿区）</param>
    Task<IReadOnlyList<AddressComponents>> GetTownsAsync(
        string prefName,
        string cityName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 都道府県データ。ja.json のトップレベル構造に対応する。
/// </summary>
public sealed class PrefectureData
{
    public IReadOnlyList<PrefectureEntry> Prefectures { get; init; } = [];
}

/// <summary>
/// 都道府県エントリ。
/// </summary>
public sealed class PrefectureEntry
{
    public int Code { get; init; }
    public string Pref { get; init; } = string.Empty;
    public GeoPoint? Point { get; init; }
    public IReadOnlyList<CityEntry> Cities { get; init; } = [];
}

/// <summary>
/// 市区町村エントリ。
/// </summary>
public sealed class CityEntry
{
    public int Code { get; init; }
    public string City { get; init; } = string.Empty;

    /// <summary>政令指定都市の区名（例：中央区）。区がない場合は null</summary>
    public string? Ward { get; init; }

    public GeoPoint? Point { get; init; }

    /// <summary>
    /// 表示用の市区町村名。Ward がある場合は City + Ward を結合する。
    /// （例：札幌市中央区）
    /// </summary>
    public string DisplayName => Ward is null ? City : $"{City}{Ward}";
}
