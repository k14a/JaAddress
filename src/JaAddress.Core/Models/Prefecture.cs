namespace JaAddress.Core.Models;

public sealed class Prefecture {
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
}
