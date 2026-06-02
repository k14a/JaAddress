namespace JaAddress.Core.Models;

public sealed class City {
    public int Code { get; init; }
    public string PrefectureName { get; init; } = string.Empty;
    /// <summary>市区町村の完全名（郡名・区名を含む）。例: "泉南郡熊取町"、"大阪市北区"</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>郡名。郡に属する町村の場合のみ設定される。例: "泉南郡"</summary>
    public string? County { get; init; }
    public string? Ward { get; init; }
    /// <summary>表示名。Name に ward/county を含んだ完全名。</summary>
    public string DisplayName => this.Name;
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
}
