namespace JaAddress.Core.Models;

/// <summary>都道府県。</summary>
public sealed class Prefecture {
    /// <summary>全国地方公共団体コード（6 桁、検査数字を含む）。例: 130001（東京都）</summary>
    public int Code { get; init; }
    /// <summary>都道府県名。例: "東京都"</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>代表点の緯度。</summary>
    public decimal Latitude { get; init; }
    /// <summary>代表点の経度。</summary>
    public decimal Longitude { get; init; }
}
