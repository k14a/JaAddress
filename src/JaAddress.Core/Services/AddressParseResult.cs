using JaAddress.Core.Models;

namespace JaAddress.Core.Services;

public sealed class AddressParseResult {
    public Prefecture Prefecture { get; init; } = null!;
    public City City { get; init; } = null!;
    /// <summary>町字まで分割できた場合</summary>
    public Town? Town { get; init; }
    /// <summary>1丁目、一丁目 など</summary>
    public string? Street { get; init; }
    /// <summary>2-3、２－３ など</summary>
    public string? Block { get; init; }
    /// <summary>SplitRemainder=falseの場合の町字以降</summary>
    public string Remainder { get; init; } = string.Empty;
}
