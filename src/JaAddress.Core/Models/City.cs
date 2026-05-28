namespace JaAddress.Core.Models;

public sealed class City {
    public int Code { get; init; }
    public string PrefectureName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Ward { get; init; }
    /// <summary>表示名。Wardがある場合は "市名区名" の形式。</summary>
    public string DisplayName => this.Ward is null ? this.Name : $"{this.Name}{this.Ward}";
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
}
