namespace JaAddress.Core.Models;

public sealed class Town {
    public string PrefectureName { get; init; } = string.Empty;
    public string CityName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Koaza { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
}
