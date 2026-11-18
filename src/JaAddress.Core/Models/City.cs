namespace JaAddress.Core.Models;

/// <summary>市区町村。政令指定都市の区は区ごとに 1 件となる。</summary>
public sealed class City {
    /// <summary>全国地方公共団体コード（6 桁、検査数字を含む）。例: 131016（千代田区）</summary>
    public int Code { get; init; }
    /// <summary>属する都道府県の名前。例: "東京都"</summary>
    public string PrefectureName { get; init; } = string.Empty;
    /// <summary>市区町村の完全名（郡名・区名を含む）。例: "泉南郡熊取町"、"大阪市北区"</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>郡名。郡に属する町村の場合のみ設定される。例: "泉南郡"</summary>
    public string? County { get; init; }
    /// <summary>政令指定都市の区名。区の場合のみ設定される。例: "北区"</summary>
    public string? Ward { get; init; }
    /// <summary>表示名。Name に ward/county を含んだ完全名。</summary>
    public string DisplayName => this.Name;
    /// <summary>代表点の緯度。</summary>
    public decimal Latitude { get; init; }
    /// <summary>代表点の経度。</summary>
    public decimal Longitude { get; init; }
}
