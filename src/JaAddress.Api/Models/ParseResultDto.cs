namespace JaAddress.Api.Models;

public sealed class ParseResultDto {
    public string Input { get; init; } = string.Empty;
    public string? Prefecture { get; init; }
    public string? City { get; init; }
    /// <summary>郡名。郡に属する町村の場合のみ設定される。例: "泉南郡"</summary>
    public string? County { get; init; }
    public string? Town { get; init; }
    /// <summary>小字。例: "字大地内"</summary>
    public string? Koaza { get; init; }
    public string? Street { get; init; }
    public string? Block { get; init; }
    public string? Remainder { get; init; }
    public int Offset { get; init; }
    public bool Corrected { get; init; }
    public bool Success { get; init; }
}
