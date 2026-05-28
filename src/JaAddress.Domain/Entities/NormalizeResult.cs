using JaAddress.Domain.Enums;
using JaAddress.Domain.ValueObjects;

namespace JaAddress.Domain.Entities;

/// <summary>
/// 住所正規化の結果を表すエンティティ。
/// geolonia/normalize-japanese-addresses の戻り値オブジェクトに対応する。
/// </summary>
public sealed class NormalizeResult {
    /// <summary>都道府県名（例：東京都）</summary>
    public string Pref { get; init; } = string.Empty;

    /// <summary>市区町村名（例：新宿区）</summary>
    public string City { get; init; } = string.Empty;

    /// <summary>大字・丁目名（例：新宿三丁目）</summary>
    public string Town { get; init; } = string.Empty;

    /// <summary>
    /// 街区符号・住居符号または地番（例：3-3）。
    /// 正規化レベルが Address(8) に達した場合に設定される。
    /// </summary>
    public string Addr { get; init; } = string.Empty;

    /// <summary>
    /// 正規化できなかった残余文字列。
    /// 建物名や部屋番号など正規化対象外の文字列が入る。
    /// </summary>
    public string Other { get; init; } = string.Empty;

    /// <summary>正規化レベル</summary>
    public NormalizeLevel Level { get; init; } = NormalizeLevel.Unknown;

    /// <summary>
    /// 位置情報データ（EPSG:4326）。
    /// 位置情報が存在しない場合は null。
    /// </summary>
    public GeoPoint? Point { get; init; }

    /// <summary>
    /// 正規化に成功したかどうか（都道府県以上を判別できた場合を成功とする）。
    /// </summary>
    public bool IsSuccess => Level >= NormalizeLevel.Prefecture;
}
