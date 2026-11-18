namespace JaAddress.Core.Models;

/// <summary>町字（大字・町・丁目の前までの名前）。</summary>
public sealed class Town {
    /// <summary>属する都道府県の名前。例: "東京都"</summary>
    public string PrefectureName { get; init; } = string.Empty;
    /// <summary>属する市区町村の完全名（<see cref="City.Name"/> と同じ表記）。例: "大阪市北区"</summary>
    public string CityName { get; init; } = string.Empty;
    /// <summary>町字名。例: "大字久保"、"丸の内"</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// 小字。現在は設定されない（常に null）。
    /// 解析で読み取った小字は <see cref="Services.AddressParseResult.Koaza"/> を参照する。
    /// </summary>
    public string? Koaza { get; init; }
    /// <summary>代表点の緯度。データにない場合は null。</summary>
    public decimal? Latitude { get; init; }
    /// <summary>代表点の経度。データにない場合は null。</summary>
    public decimal? Longitude { get; init; }
}
