namespace JaAddress.Api.Models;

public sealed class ParseResultDto {
    public string Input { get; init; } = string.Empty;
    public string? Prefecture { get; init; }
    public string? City { get; init; }
    public string? Town { get; init; }
    public string? Street { get; init; }
    public string? Block { get; init; }
    public string? Remainder { get; init; }
    public bool Success { get; init; }
}
